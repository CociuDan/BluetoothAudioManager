using BluetoothAudioManager.Shared.Enums;
using BluetoothAudioManager.Shared.Helpers;
using BluetoothAudioManager.Shared.Models;
using BluetoothAudioManager.Updater.Models;
using BluetoothAudioManager.Updater.Services;
using Microsoft.Extensions.Hosting;

namespace BluetoothAudioManager.Updater.Workers;

/// <summary>
/// The service's work: checks GitHub on a schedule and when the app asks, keeps a newer version downloaded and checked,
/// and installs it when the installed app asks.
/// </summary>
public sealed class UpdateWorker : BackgroundService
{
    private readonly GitHubReleases _github = new();
    private readonly SemaphoreSlim _checkGate = new(1, 1);

    // Versions that failed to install, so the app isn't offered them again until the updater restarts.
    private readonly HashSet<string> _failedVersions = [];
    private UpdateStatus _status = new(UpdateState.None);
    private string? _readyPackage;
    private int _installing;

    private static string DownloadsFolder => Path.Combine(InstallLayout.DataFolder, "downloads");

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        SecureFolder.Ensure(InstallLayout.DataFolder);
        Directory.CreateDirectory(DownloadsFolder);
        UpdaterLog.Write($"Started. Installed version {InstallLayout.InstalledVersion ?? "unknown"}.");

        var pipe = PipeServer.RunAsync(HandleAsync, stop);

        try
        {
            while (!stop.IsCancellationRequested)
            {
                await CheckAsync(stop);
                await Task.Delay(UpdaterSettings.Load().CheckEvery, stop);
            }
        }
        catch (OperationCanceledException)
        {
            // The service is stopping.
        }

        await pipe;
    }

    private async Task<string> HandleAsync(string line, PipeClient client, CancellationToken stop)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        switch (parts)
        {
            case [UpdaterPipe.StatusCommand]:
                return UpdaterPipe.Format(_status);

            case [UpdaterPipe.CheckCommand]:
                return UpdaterPipe.Format(await CheckAsync(stop));

            case [UpdaterPipe.InstallCommand, .. var options]:
                return StartInstall(client, options.Contains(UpdaterPipe.ShowWindowOption))
                    ? UpdaterPipe.OkReply
                    : UpdaterPipe.Format(_status);

            default:
                return UpdaterPipe.Format(new UpdateStatus(UpdateState.Failed));
        }
    }

    private async Task<UpdateStatus> CheckAsync(CancellationToken stop)
    {
        await _checkGate.WaitAsync(stop);
        try
        {
            var installed = InstallLayout.InstalledVersion;
            var latest = await _github.LatestAsync(stop);

            if (latest is null || !Versions.IsNewer(latest.Version, installed) || _failedVersions.Contains(latest.Version))
            {
                _status = new UpdateStatus(UpdateState.None);
                _readyPackage = null;
                return _status;
            }

            var path = Path.Combine(DownloadsFolder, $"BluetoothAudioManager-{latest.Version}.zip");
            await _github.DownloadAsync(latest, path, stop);

            if (_status.Version != latest.Version)
            {
                UpdaterLog.Write($"Version {latest.Version} is downloaded and ready.");
                DeleteOtherDownloads(path);
            }

            _readyPackage = path;
            _status = new UpdateStatus(UpdateState.Ready, latest.Version);
            return _status;
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            UpdaterLog.Write($"Couldn't check for updates: {exception.Message}");

            // A version already downloaded can still be installed.
            return _status.State == UpdateState.Ready ? _status : new UpdateStatus(UpdateState.Failed);
        }
        finally
        {
            _checkGate.Release();
        }
    }

    /// <summary>Starts installing the ready version, if there is one and the installed app is the one asking.</summary>
    private bool StartInstall(PipeClient client, bool showWindow)
    {
        if (_status is not { State: UpdateState.Ready, Version: { } version }
            || _readyPackage is not { } package
            || InstallLayout.InstallFolder is not { } installFolder
            || !IsInstalledApp(client, installFolder)
            || Interlocked.Exchange(ref _installing, 1) == 1)
        {
            return false;
        }

        UpdaterLog.Write($"Installing version {version}, asked from session {client.SessionId}.");
        _ = Task.Run(async () =>
        {
            try
            {
                await UpdateInstaller.InstallAppAsync(installFolder, package, version, CancellationToken.None);
                _status = new UpdateStatus(UpdateState.None);
                _readyPackage = null;
                UpdateInstaller.OpenApp(installFolder, client.SessionId, showWindow);

                // Last, as it stops this service to replace it.
                UpdateInstaller.ReplaceUpdater(installFolder, package);
            }
            catch (Exception exception)
            {
                UpdaterLog.Write($"Couldn't install version {version}: {exception.Message}");
                _failedVersions.Add(version);
                _status = new UpdateStatus(UpdateState.None);

                // The app closed for the update, so the version that's still installed opens again.
                UpdateInstaller.OpenApp(installFolder, client.SessionId, showWindow);
            }
            finally
            {
                Interlocked.Exchange(ref _installing, 0);
            }
        });

        return true;
    }

    // Only the installed app may ask for an install, not any program that finds the pipe.
    private static bool IsInstalledApp(PipeClient client, string installFolder) =>
        string.Equals(
            client.ExecutablePath,
            Path.Combine(InstallLayout.AppFolder(installFolder), InstallLayout.AppExeName),
            StringComparison.OrdinalIgnoreCase);

    private static void DeleteOtherDownloads(string keep)
    {
        foreach (var file in Directory.EnumerateFiles(DownloadsFolder))
        {
            if (!string.Equals(file, keep, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception)
                {
                    // Tried again after the next download.
                }
            }
        }
    }

    public override void Dispose()
    {
        _github.Dispose();
        _checkGate.Dispose();
        base.Dispose();
    }
}

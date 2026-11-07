using System.Diagnostics;
using BluetoothAudioManager.Shared.Enums;
using BluetoothAudioManager.Shared.Helpers;
using BluetoothAudioManager.Shared.Models;
using BluetoothAudioManager.Updater.Models;
using BluetoothAudioManager.Updater.Services;
using Microsoft.Extensions.Hosting;

namespace BluetoothAudioManager.Updater.Workers;

/// <summary>
/// The service's work: checks GitHub on a schedule and when the app asks, keeps the latest version's installer
/// downloaded and checked in the install folder's hidden .update folder, and runs it to update when the installed app asks.
/// </summary>
public sealed class UpdateWorker : BackgroundService
{
    private readonly GitHubReleases _github = new();
    private readonly SemaphoreSlim _checkGate = new(1, 1);
    private UpdateStatus _status = new(UpdateState.None);
    private ReleaseSetup? _ready;
    private int _installing;

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        SecureFolder.Ensure(InstallLayout.DataFolder);
        UpdaterLog.Write($"Started. Installed version {InstallLayout.InstalledVersion ?? "unknown"}.");

        var pipe = PipeServer.RunAsync(HandleAsync, stop);

        try
        {
            while (!stop.IsCancellationRequested)
            {
                // Turned off in the app's settings, it only checks when the app asks.
                var settings = UpdaterSettings.Load();
                if (settings.CheckAutomatically)
                {
                    await CheckAsync(stop);
                }

                await Task.Delay(settings.CheckEvery, stop);
            }
        }
        catch (OperationCanceledException)
        {
            // The service is stopping.
        }

        await pipe;
    }

    private static string? SetupPath =>
        InstallLayout.InstallFolder is { } installFolder
            ? Path.Combine(InstallLayout.UpdateFolder(installFolder), InstallLayout.SetupFileName)
            : null;

    private async Task<string> HandleAsync(string line, PipeClient client, CancellationToken stop)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        switch (parts)
        {
            case [UpdaterPipe.StatusCommand]:
                return UpdaterPipe.Format(_status);

            case [UpdaterPipe.CheckCommand]:
                return UpdaterPipe.Format(await CheckAsync(stop));

            case [UpdaterPipe.ReleaseNotesCommand]:
                var release = await _github.LatestReleaseNotesAsync(stop);
                return release is null
                    ? UpdaterPipe.ReleaseNotesUnavailableReply
                    : UpdaterPipe.FormatReleaseNotes(release.Value.Version, release.Value.Notes);

            case [UpdaterPipe.InstallCommand, .. var options]:
                return await StartInstallAsync(client, options.Contains(UpdaterPipe.ShowWindowOption), stop)
                    ? UpdaterPipe.OkReply
                    : UpdaterPipe.Format(_status);

            case [UpdaterPipe.AutomaticCommand]:
                return UpdaterSettings.Load().CheckAutomatically ? UpdaterPipe.OnOption : UpdaterPipe.OffOption;

            case [UpdaterPipe.AutomaticCommand, UpdaterPipe.OnOption or UpdaterPipe.OffOption]:
                return SetAutomatic(client, parts[1] == UpdaterPipe.OnOption, stop)
                    ? UpdaterPipe.OkReply
                    : UpdaterPipe.Format(new UpdateStatus(UpdateState.Failed));

            case [UpdaterPipe.StartupCommand, UpdaterPipe.OnOption]:
                return TurnStartupBackOn(client)
                    ? UpdaterPipe.OkReply
                    : UpdaterPipe.Format(new UpdateStatus(UpdateState.Failed));

            default:
                return UpdaterPipe.Format(new UpdateStatus(UpdateState.Failed));
        }
    }

    /// <summary>
    /// Clears Windows' record that the app was turned off for everyone in Task Manager's Startup apps tab, if the installed
    /// app is the one asking. Each person's own choice in the app still decides whether it stays open for them.
    /// </summary>
    private static bool TurnStartupBackOn(PipeClient client)
    {
        if (InstallLayout.InstallFolder is not { } installFolder || !IsInstalledApp(client, installFolder))
        {
            return false;
        }

        try
        {
            using var machine = InstallLayout.OpenMachineKey();
            using var approved = machine.OpenSubKey(InstallLayout.StartupApprovedKeyPath, writable: true);
            approved?.DeleteValue(InstallLayout.RunValueName, throwOnMissingValue: false);
            UpdaterLog.Write($"Starting with the system turned back on for everyone, from session {client.SessionId}.");
            return true;
        }
        catch (Exception exception)
        {
            UpdaterLog.Write($"Couldn't turn starting with the system back on: {exception.Message}");
            return false;
        }
    }

    private async Task<UpdateStatus> CheckAsync(CancellationToken stop)
    {
        await _checkGate.WaitAsync(stop);
        try
        {
            var latest = await _github.LatestAsync(stop);

            // A version that failed to install isn't offered again, until a newer one comes out.
            if (latest is null
                || !Versions.IsNewer(latest.Version, InstallLayout.InstalledVersion)
                || latest.Version == InstallLayout.FailedVersion
                || SetupPath is not { } path)
            {
                _status = new UpdateStatus(UpdateState.None);
                _ready = null;
                return _status;
            }

            // A newer download always replaces the one before, as there's only ever this one file.
            PrepareUpdateFolder(Path.GetDirectoryName(path)!);
            await _github.DownloadAsync(latest, path, stop);

            string notes;
            try
            {
                notes = await _github.ReleaseNotesAsync(latest, stop);
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                UpdaterLog.Write($"Couldn't fetch release notes for version {latest.Version}: {exception.Message}");
                notes = "Changelog isn't available right now.";
            }

            if (_status.Version != latest.Version)
            {
                UpdaterLog.Write($"Version {latest.Version} is downloaded and ready.");
            }

            _ready = latest;
            _status = new UpdateStatus(UpdateState.Ready, latest.Version, notes);
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

    /// <summary>
    /// Runs the downloaded installer to update, if a version is ready and the installed app is the one asking.
    /// The installer waits for the app to close, stops this service, swaps in the new version, then starts the service
    /// again and opens the app for the person who asked.
    /// </summary>
    private async Task<bool> StartInstallAsync(PipeClient client, bool showWindow, CancellationToken stop)
    {
        if (_ready is not { } ready
            || SetupPath is not { } path
            || InstallLayout.InstallFolder is not { } installFolder
            || !IsInstalledApp(client, installFolder)
            || Interlocked.Exchange(ref _installing, 1) == 1)
        {
            return false;
        }

        try
        {
            // Checked again just before it runs with administrator rights.
            if (!await GitHubReleases.MatchesAsync(path, ready.Sha256, stop))
            {
                UpdaterLog.Write($"The downloaded {ready.Version} installer changed, so it wasn't run.");
                _status = new UpdateStatus(UpdateState.None);
                _ready = null;
                return false;
            }

            var start = new ProcessStartInfo(path) { UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add(InstallLayout.UpdateArgument);
            start.ArgumentList.Add(InstallLayout.SessionOption);
            start.ArgumentList.Add(client.SessionId.ToString());
            if (showWindow)
            {
                start.ArgumentList.Add(InstallLayout.WindowOption);
            }

            Process.Start(start)?.Dispose();
            UpdaterLog.Write($"Installing version {ready.Version}, asked from session {client.SessionId}.");
            return true;
        }
        catch (Exception exception)
        {
            UpdaterLog.Write($"Couldn't start installing version {ready.Version}: {exception.Message}");
            return false;
        }
        finally
        {
            Interlocked.Exchange(ref _installing, 0);
        }
    }

    /// <summary>
    /// Turns checking by itself on or off for everyone on the PC, if the installed app is the one asking.
    /// Turned on, it checks straight away. Turned off, anything already downloaded stops being offered.
    /// </summary>
    private bool SetAutomatic(PipeClient client, bool on, CancellationToken stop)
    {
        if (InstallLayout.InstallFolder is not { } installFolder || !IsInstalledApp(client, installFolder))
        {
            return false;
        }

        var settings = UpdaterSettings.Load();
        settings.CheckAutomatically = on;
        settings.Save();
        UpdaterLog.Write($"Checking for updates by itself turned {(on ? "on" : "off")}, from session {client.SessionId}.");

        if (on)
        {
            _ = Task.Run(() => CheckAsync(stop), stop);
        }
        else
        {
            _status = new UpdateStatus(UpdateState.None);
            _ready = null;
        }

        return true;
    }

    // Only the installed app may ask for an install or change a setting, not any program that finds the pipe.
    private static bool IsInstalledApp(PipeClient client, string installFolder) =>
        string.Equals(
            client.ExecutablePath,
            Path.Combine(InstallLayout.AppFolder(installFolder), InstallLayout.AppExeName),
            StringComparison.OrdinalIgnoreCase);

    // Hidden, and only administrators and Windows may change it, as what's in it runs with administrator rights.
    private static void PrepareUpdateFolder(string folder)
    {
        SecureFolder.Ensure(folder);
        var directory = new DirectoryInfo(folder);
        directory.Attributes |= FileAttributes.Hidden;
    }

    public override void Dispose()
    {
        _github.Dispose();
        _checkGate.Dispose();
        base.Dispose();
    }
}

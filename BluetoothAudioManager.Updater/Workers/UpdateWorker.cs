using System.Diagnostics;
using BluetoothAudioManager.Shared.Enums;
using BluetoothAudioManager.Shared.Helpers;
using BluetoothAudioManager.Shared.Models;
using BluetoothAudioManager.Updater.Models;
using BluetoothAudioManager.Updater.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Win32;

namespace BluetoothAudioManager.Updater.Workers;

/// <summary>
/// The service's work: checks GitHub on a schedule and when the app asks, keeps the latest version's installer
/// downloaded and checked in the install folder's hidden .update folder with its app unpacked beside it, and swaps
/// that in to update when the installed app asks.
/// </summary>
public sealed class UpdateWorker : BackgroundService
{
    private const int MoveAttempts = 20;

    private static readonly TimeSpan AppCloseCheckEvery = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan MoveRetryEvery = TimeSpan.FromMilliseconds(250);

    private readonly GitHubReleases _github = new();
    private readonly SemaphoreSlim _checkGate = new(1, 1);
    private UpdateStatus _status = new(UpdateState.None);
    private ReleaseSetup? _ready;
    private int _installing;
    private int _preparing;

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

            case [UpdaterPipe.StartupCommand, UpdaterPipe.OnOption or UpdaterPipe.OffOption]:
                return SetStartup(client, parts[1] == UpdaterPipe.OnOption)
                    ? UpdaterPipe.OkReply
                    : UpdaterPipe.Format(new UpdateStatus(UpdateState.Failed));

            default:
                return UpdaterPipe.Format(new UpdateStatus(UpdateState.Failed));
        }
    }

    /// <summary>
    /// Turns starting with the system on or off for everyone on the PC, in the same record Task Manager's Startup apps
    /// tab keeps, if the installed app is the one asking, so the app and Task Manager always agree.
    /// </summary>
    private static bool SetStartup(PipeClient client, bool on)
    {
        if (InstallLayout.InstallFolder is not { } installFolder || !IsInstalledApp(client, installFolder))
        {
            return false;
        }

        try
        {
            using var machine = InstallLayout.OpenMachineKey();
            using var approved = machine.CreateSubKey(InstallLayout.StartupApprovedKeyPath);
            approved.SetValue(InstallLayout.RunValueName, StartupApproval.Value(on, DateTime.UtcNow), RegistryValueKind.Binary);
            UpdaterLog.Write($"Starting with the system turned {(on ? "on" : "off")} for everyone, from session {client.SessionId}.");
            return true;
        }
        catch (Exception exception)
        {
            UpdaterLog.Write($"Couldn't turn starting with the system {(on ? "on" : "off")}: {exception.Message}");
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
            StartPreparing(latest, path, stop);
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
    /// Has the downloaded installer unpack the new app into the .update folder in the background, so updating later
    /// only swaps folders. Until that's done, or if it fails, updating takes the longer way.
    /// </summary>
    private void StartPreparing(ReleaseSetup setup, string path, CancellationToken stop)
    {
        if (InstallLayout.InstallFolder is not { } installFolder
            || InstallLayout.StagedVersion(installFolder) == setup.Version
            || Interlocked.Exchange(ref _preparing, 1) == 1)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                // Checked again just before it runs with administrator rights.
                if (await GitHubReleases.MatchesAsync(path, setup.Sha256, stop))
                {
                    using var process = Process.Start(SetupStart(path, InstallLayout.PrepareArgument));
                    if (process is not null)
                    {
                        await process.WaitForExitAsync(stop);
                    }
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                UpdaterLog.Write($"Couldn't unpack version {setup.Version} ahead of time: {exception.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _preparing, 0);
            }
        }, stop);
    }

    /// <summary>
    /// Updates, if a version is ready and the installed app is the one asking. With the new app already unpacked, this
    /// service waits for the app to close, swaps the app's folder for it, opens the app for the person who asked, and
    /// leaves the installer to replace the updater in the background. Otherwise the installer does it all.
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

        var handedOver = false;
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

            UpdaterLog.Write($"Installing version {ready.Version}, asked from session {client.SessionId}.");

            if (Volatile.Read(ref _preparing) == 0 && InstallLayout.StagedVersion(installFolder) == ready.Version)
            {
                // Answered first, so the app closes, which the swap waits for.
                _ = Task.Run(() => SwapInPreparedApp(path, installFolder, client.SessionId, showWindow, ready.Version));
                handedOver = true;
                return true;
            }

            StartFullUpdate(path, client.SessionId, showWindow);
            return true;
        }
        catch (Exception exception)
        {
            UpdaterLog.Write($"Couldn't start installing version {ready.Version}: {exception.Message}");
            return false;
        }
        finally
        {
            if (!handedOver)
            {
                Interlocked.Exchange(ref _installing, 0);
            }
        }
    }

    private void SwapInPreparedApp(string setupPath, string installFolder, uint sessionId, bool showWindow, string version)
    {
        var timer = Stopwatch.StartNew();
        try
        {
            WaitForAppToClose();
            UpdaterLog.Write($"The app closed, {timer.ElapsedMilliseconds} ms after Update now.");

            var appFolder = InstallLayout.AppFolder(installFolder);
            var oldFolder = appFolder + InstallLayout.OldFolderSuffix;
            DeleteIfThere(oldFolder);
            MoveFolder(appFolder, oldFolder);
            try
            {
                MoveFolder(InstallLayout.StagedAppFolder(installFolder), appFolder);
            }
            catch (Exception)
            {
                MoveFolder(oldFolder, appFolder);
                throw;
            }

            UpdaterLog.Write($"Swapped in version {version}, {timer.ElapsedMilliseconds} ms after Update now.");
            OpenApp(appFolder, sessionId, showWindow);
            UpdaterLog.Write($"Opened the app, {timer.ElapsedMilliseconds} ms after Update now.");
            DeleteIfThere(oldFolder);

            // The rest happens with the app already open: the installer stops this service, replaces it, records
            // the new version and starts it again.
            Process.Start(SetupStart(setupPath, InstallLayout.FinishArgument))?.Dispose();
        }
        catch (Exception exception)
        {
            UpdaterLog.Write($"Couldn't swap in version {version} quickly, so it updates the longer way: {exception.Message}");
            try
            {
                StartFullUpdate(setupPath, sessionId, showWindow);
            }
            catch (Exception fallback)
            {
                UpdaterLog.Write($"Couldn't start installing version {version}: {fallback.Message}");
            }
        }
        finally
        {
            Interlocked.Exchange(ref _installing, 0);
        }
    }

    // The installer does it all: waits for the app, stops this service, unpacks and swaps both folders, then opens the app.
    private static void StartFullUpdate(string setupPath, uint sessionId, bool showWindow)
    {
        var start = SetupStart(setupPath, InstallLayout.UpdateArgument);
        start.ArgumentList.Add(InstallLayout.SessionOption);
        start.ArgumentList.Add(sessionId.ToString());
        if (showWindow)
        {
            start.ArgumentList.Add(InstallLayout.WindowOption);
        }

        Process.Start(start)?.Dispose();
    }

    private static ProcessStartInfo SetupStart(string setupPath, string argument)
    {
        var start = new ProcessStartInfo(setupPath) { UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add(argument);
        return start;
    }

    // The app that asked closes within moments. Copies open for other signed-in people are waited for.
    private static void WaitForAppToClose()
    {
        var since = Stopwatch.StartNew();
        var logged = false;
        while (Process.GetProcessesByName(InstallLayout.AppProcessName) is { Length: > 0 } running)
        {
            Array.ForEach(running, process => process.Dispose());
            if (!logged && since.Elapsed > TimeSpan.FromMinutes(1))
            {
                UpdaterLog.Write("Waiting for the app to close for everyone signed in.");
                logged = true;
            }

            Thread.Sleep(AppCloseCheckEvery);
        }
    }

    // Opened as the person who asked, in their session, not with this service's rights.
    private static void OpenApp(string appFolder, uint sessionId, bool showWindow)
    {
        try
        {
            var arguments = showWindow ? InstallLayout.UpdatedArgument : $"{InstallLayout.TrayArgument} {InstallLayout.UpdatedArgument}";
            UserSessionLauncher.Start(sessionId, Path.Combine(appFolder, InstallLayout.AppExeName), arguments);
        }
        catch (Exception exception)
        {
            // They may have signed out meanwhile. The app starts as usual next time.
            UpdaterLog.Write($"Couldn't open the app again: {exception.Message}");
        }
    }

    // Files can stay locked for a moment after a program closes, such as while antivirus looks at them.
    private static void MoveFolder(string from, string to)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                Directory.Move(from, to);
                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException && attempt < MoveAttempts)
            {
                Thread.Sleep(MoveRetryEvery);
            }
        }
    }

    private static void DeleteIfThere(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception exception)
        {
            // Left for the next update, or the uninstaller, to clear.
            UpdaterLog.Write($"Couldn't delete {folder}: {exception.Message}");
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

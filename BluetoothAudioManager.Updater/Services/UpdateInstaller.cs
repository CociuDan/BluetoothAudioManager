using System.Diagnostics;
using System.IO.Compression;
using BluetoothAudioManager.Shared.Helpers;

namespace BluetoothAudioManager.Updater.Services;

/// <summary>
/// Installs a downloaded package: swaps in the new app once every copy of it has closed, puts the old one back if
/// that fails, and hands the updater's own new files to a copy of it that replaces this service.
/// </summary>
internal static class UpdateInstaller
{
    private static readonly TimeSpan CheckEvery = TimeSpan.FromMilliseconds(500);
    private const int MoveAttempts = 20;

    /// <summary>Folders the app's swap uses beside the app folder.</summary>
    private const string NewSuffix = ".new";
    private const string OldSuffix = ".old";

    /// <summary>Waits for the app to close everywhere, then installs the package's app. Throws if it couldn't, with the old app back in place.</summary>
    public static async Task InstallAppAsync(string installFolder, string packagePath, string version, CancellationToken stop)
    {
        var appFolder = InstallLayout.AppFolder(installFolder);
        var newFolder = appFolder + NewSuffix;
        var oldFolder = appFolder + OldSuffix;

        // Unpacked before waiting, so the app is closed for as short a time as possible.
        using (var archive = ZipFile.OpenRead(packagePath))
        {
            PackageArchive.ExtractFolder(archive, InstallLayout.AppFolderName, newFolder);
        }

        await WaitForAppToCloseAsync(stop);

        DeleteIfThere(oldFolder);
        await MoveAsync(appFolder, oldFolder, stop);

        try
        {
            await MoveAsync(newFolder, appFolder, stop);
        }
        catch (Exception)
        {
            await MoveAsync(oldFolder, appFolder, stop);
            throw;
        }

        DeleteIfThere(oldFolder);
        RecordVersion(version);
        UpdaterLog.Write($"Installed the app, version {version}.");
    }

    /// <summary>
    /// Unpacks the package's updater to a staging folder and starts it there to replace this service, which it stops
    /// and starts again. A service can't overwrite its own running files, hence the copy.
    /// </summary>
    public static void ReplaceUpdater(string installFolder, string packagePath)
    {
        var staging = Path.Combine(InstallLayout.DataFolder, "staging", InstallLayout.UpdaterFolderName);
        using (var archive = ZipFile.OpenRead(packagePath))
        {
            PackageArchive.ExtractFolder(archive, InstallLayout.UpdaterFolderName, staging);
        }

        var start = new ProcessStartInfo(Path.Combine(staging, InstallLayout.UpdaterExeName)) { UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add(ServiceReplacer.Argument);
        start.ArgumentList.Add(installFolder);
        Process.Start(start)?.Dispose();
    }

    /// <summary>Opens the app for the person who asked for the update, in their Windows session.</summary>
    public static void OpenApp(string installFolder, uint sessionId, bool showWindow)
    {
        try
        {
            var appPath = Path.Combine(InstallLayout.AppFolder(installFolder), InstallLayout.AppExeName);
            UserSessionLauncher.Start(sessionId, appPath, showWindow ? "" : InstallLayout.TrayArgument);
        }
        catch (Exception exception)
        {
            // They may have signed out meanwhile. The app starts as usual next time.
            UpdaterLog.Write($"Couldn't open the app again: {exception.Message}");
        }
    }

    private static async Task WaitForAppToCloseAsync(CancellationToken stop)
    {
        var waited = TimeSpan.Zero;
        while (Process.GetProcessesByName(InstallLayout.AppProcessName) is { Length: > 0 } running)
        {
            Array.ForEach(running, process => process.Dispose());

            // The app that asked closes within moments. Copies open for other signed-in people are waited for.
            if (waited == TimeSpan.FromMinutes(1))
            {
                UpdaterLog.Write("Waiting for the app to close for everyone signed in.");
            }

            await Task.Delay(CheckEvery, stop);
            waited += CheckEvery;
        }
    }

    // Files can stay locked for a moment after the app closes, such as while antivirus looks at them.
    private static async Task MoveAsync(string from, string to, CancellationToken stop)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                Directory.Move(from, to);
                return;
            }
            catch (IOException) when (attempt < MoveAttempts)
            {
                await Task.Delay(CheckEvery, stop);
            }
            catch (UnauthorizedAccessException) when (attempt < MoveAttempts)
            {
                await Task.Delay(CheckEvery, stop);
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
            UpdaterLog.Write($"Couldn't delete {folder}: {exception.Message}");
        }
    }

    // Shown in Installed apps, and compared with the next release.
    private static void RecordVersion(string version)
    {
        using var machine = InstallLayout.OpenMachineKey();
        using (var key = machine.CreateSubKey(InstallLayout.RegistryKeyPath))
        {
            key.SetValue(InstallLayout.VersionValue, version);
        }

        using (var uninstall = machine.OpenSubKey(InstallLayout.UninstallKeyPath, writable: true))
        {
            uninstall?.SetValue("DisplayVersion", version);
        }
    }
}

using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using BluetoothAudioManager.Shared.Helpers;

namespace BluetoothAudioManager.Installer.Services;

/// <summary>
/// The installer's update modes, run by the updater service from the hidden .update folder, without any pages.
/// Usually the service has already swapped in the app unpacked ahead of time and opened it, and Finish replaces the
/// updater in the background. Otherwise Run does it all: it waits for the app to close everywhere, stops the service,
/// swaps in this version's app and updater, opens the app for the person who asked, then starts the service again.
/// If the swap fails, the old version stays.
/// </summary>
internal static class SilentUpdate
{
    private static readonly TimeSpan CheckEvery = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Replaces the updater and records the new version, after the service swapped in the new app and opened it.
    /// The service stops meanwhile and starts again afterwards, which the open app doesn't notice.
    /// </summary>
    public static void Finish()
    {
        if (InstallLayout.InstallFolder is not { } installFolder)
        {
            return;
        }

        try
        {
            UpdaterService.Stop();

            using (var package = AppInstaller.OpenPackage())
            using (var archive = new ZipArchive(package, ZipArchiveMode.Read))
            {
                FolderSwap.Replace(archive, InstallLayout.UpdaterFolderName, InstallLayout.UpdaterFolder(installFolder));
            }

            File.Copy(Environment.ProcessPath!, Path.Combine(installFolder, InstallLayout.UninstallerName), overwrite: true);
            AppInstaller.Register(installFolder);
            UpdaterLog.Write($"Updated to version {AppInstaller.Version}.");
        }
        catch (Exception exception)
        {
            // The new app is already in place and runs with the updater it has, which a later update replaces.
            UpdaterLog.Write($"Couldn't finish updating to version {AppInstaller.Version}: {exception.Message}");
        }
        finally
        {
            StartService();
            InstalledFiles.DeleteFolder(InstallLayout.StagedFolder(installFolder));
        }
    }

    /// <param name="sessionId">The Windows session to open the app in afterwards, or null to leave it closed.</param>
    /// <param name="showWindow">Whether the app opens with its window showing, as it was.</param>
    public static void Run(uint? sessionId, bool showWindow)
    {
        if (InstallLayout.InstallFolder is not { } installFolder)
        {
            UpdaterLog.Write("Couldn't update: the app isn't installed.");
            return;
        }

        try
        {
            WaitForAppToClose();
            UpdaterService.Stop();

            using (var package = AppInstaller.OpenPackage())
            using (var archive = new ZipArchive(package, ZipArchiveMode.Read))
            {
                FolderSwap.Replace(archive, InstallLayout.AppFolderName, InstallLayout.AppFolder(installFolder));
                FolderSwap.Replace(archive, InstallLayout.UpdaterFolderName, InstallLayout.UpdaterFolder(installFolder));
            }

            // This installer, copied in, is the uninstaller from now on.
            File.Copy(Environment.ProcessPath!, Path.Combine(installFolder, InstallLayout.UninstallerName), overwrite: true);
            AppInstaller.Register(installFolder);
            UpdaterLog.Write($"Updated to version {AppInstaller.Version}.");
        }
        catch (Exception exception)
        {
            UpdaterLog.Write($"Couldn't update to version {AppInstaller.Version}: {exception.Message}");
            AppInstaller.RecordFailedVersion();
        }
        finally
        {
            // The app first, as the person waits for it, and it doesn't need the updater to open.
            if (sessionId is { } session)
            {
                OpenApp(installFolder, session, showWindow);
            }

            StartService();
        }
    }

    private static void StartService()
    {
        try
        {
            UpdaterService.Start();
        }
        catch (Exception exception)
        {
            // Windows starts it again at the next restart.
            UpdaterLog.Write($"Couldn't start the updater again: {exception.Message}");
        }
    }

    // The app that asked closes within moments. Copies open for other signed-in people are waited for.
    private static void WaitForAppToClose()
    {
        var waited = TimeSpan.Zero;
        while (Process.GetProcessesByName(InstallLayout.AppProcessName) is { Length: > 0 } running)
        {
            Array.ForEach(running, process => process.Dispose());

            if (waited >= TimeSpan.FromMinutes(1) && waited - CheckEvery < TimeSpan.FromMinutes(1))
            {
                UpdaterLog.Write("Waiting for the app to close for everyone signed in.");
            }

            Thread.Sleep(CheckEvery);
            waited += CheckEvery;
        }
    }

    // Opened as the person who asked, in their session, not with the update's administrator rights.
    private static void OpenApp(string installFolder, uint sessionId, bool showWindow)
    {
        try
        {
            var appPath = Path.Combine(InstallLayout.AppFolder(installFolder), InstallLayout.AppExeName);
            var arguments = showWindow ? InstallLayout.UpdatedArgument : $"{InstallLayout.TrayArgument} {InstallLayout.UpdatedArgument}";
            UserSessionLauncher.Start(sessionId, appPath, arguments);
        }
        catch (Exception exception)
        {
            // They may have signed out meanwhile. The app starts as usual next time.
            UpdaterLog.Write($"Couldn't open the app again: {exception.Message}");
        }
    }
}

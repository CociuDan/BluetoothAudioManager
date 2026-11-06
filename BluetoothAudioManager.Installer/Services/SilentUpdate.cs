using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using BluetoothAudioManager.Shared.Helpers;

namespace BluetoothAudioManager.Installer.Services;

/// <summary>
/// The installer's update mode, run by the updater service from the hidden .update folder, without any pages.
/// It waits for the app to close everywhere, stops the service, swaps in this version's app and updater, then starts
/// the service again and opens the app for the person who asked. If the swap fails, the old version stays.
/// </summary>
internal static class SilentUpdate
{
    private static readonly TimeSpan CheckEvery = TimeSpan.FromMilliseconds(500);

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
            try
            {
                UpdaterService.Start();
            }
            catch (Exception exception)
            {
                // Windows starts it again at the next restart.
                UpdaterLog.Write($"Couldn't start the updater again: {exception.Message}");
            }

            if (sessionId is { } session)
            {
                OpenApp(installFolder, session, showWindow);
            }
        }
    }

    // The app that asked closes within moments. Copies open for other signed-in people are waited for.
    private static void WaitForAppToClose()
    {
        var waited = TimeSpan.Zero;
        while (Process.GetProcessesByName(InstallLayout.AppProcessName) is { Length: > 0 } running)
        {
            Array.ForEach(running, process => process.Dispose());

            if (waited == TimeSpan.FromMinutes(1))
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
            UserSessionLauncher.Start(sessionId, appPath, showWindow ? "" : InstallLayout.TrayArgument);
        }
        catch (Exception exception)
        {
            // They may have signed out meanwhile. The app starts as usual next time.
            UpdaterLog.Write($"Couldn't open the app again: {exception.Message}");
        }
    }
}

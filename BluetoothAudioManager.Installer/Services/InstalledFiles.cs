using System.IO;
using BluetoothAudioManager.Shared.Helpers;

namespace BluetoothAudioManager.Installer.Services;

/// <summary>The files the installer puts in the install folder, and nothing else there.</summary>
internal static class InstalledFiles
{
    /// <summary>
    /// Removes the app, the updater and the uninstaller from the folder, with any folders left over from an update,
    /// then the folder itself if nothing else is in it.
    /// </summary>
    public static void Remove(string installFolder)
    {
        var appFolder = InstallLayout.AppFolder(installFolder);
        foreach (var folder in new[] { appFolder, appFolder + ".new", appFolder + ".old", InstallLayout.UpdaterFolder(installFolder) })
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        var uninstaller = Path.Combine(installFolder, InstallLayout.UninstallerName);
        if (File.Exists(uninstaller))
        {
            File.Delete(uninstaller);
        }

        if (Directory.Exists(installFolder) && !Directory.EnumerateFileSystemEntries(installFolder).Any())
        {
            Directory.Delete(installFolder);
        }
    }
}

using System.IO;
using BluetoothAudioManager.Installer.Interop;
using BluetoothAudioManager.Shared.Helpers;

namespace BluetoothAudioManager.Installer.Services;

/// <summary>The files the installer puts in the install folder, and nothing else there.</summary>
internal static class InstalledFiles
{
    private const int DeleteAttempts = 10;

    private static readonly TimeSpan RetryEvery = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Removes the app, the updater, the downloaded installer and the uninstaller from the folder, with any folders
    /// left over from an update, then the folder itself if nothing else is in it. A file still locked after a few
    /// seconds, such as one a closing program hasn't let go of yet, is left for Windows to delete when it restarts.
    /// </summary>
    public static void Remove(string installFolder)
    {
        var appFolder = InstallLayout.AppFolder(installFolder);
        var updaterFolder = InstallLayout.UpdaterFolder(installFolder);
        string[] folders =
        [
            appFolder, .. FolderSwap.Leftovers(appFolder),
            updaterFolder, .. FolderSwap.Leftovers(updaterFolder),
            InstallLayout.UpdateFolder(installFolder),
        ];

        foreach (var folder in folders)
        {
            DeleteFolder(folder);
        }

        DeleteFile(Path.Combine(installFolder, InstallLayout.UninstallerName));

        if (Directory.Exists(installFolder) && !Directory.EnumerateFileSystemEntries(installFolder).Any())
        {
            Directory.Delete(installFolder);
        }
        else if (Directory.Exists(installFolder))
        {
            // Emptied by the restart, if what's left is only what's waiting to be deleted then.
            DeleteAtRestart(installFolder);
        }
    }

    /// <summary>Deletes the folder and everything in it, waiting a little for locked files, and leaving any still locked for the next restart.</summary>
    public static void DeleteFolder(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            DeleteFile(file);
        }

        // Deepest first, so each folder is empty by the time it's reached.
        var subfolders = Directory.EnumerateDirectories(folder, "*", SearchOption.AllDirectories)
            .OrderByDescending(path => path.Length)
            .Append(folder);

        foreach (var subfolder in subfolders)
        {
            try
            {
                Directory.Delete(subfolder);
            }
            catch (Exception)
            {
                DeleteAtRestart(subfolder);
            }
        }
    }

    private static void DeleteFile(string file)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                if (!File.Exists(file))
                {
                    return;
                }

                // A read-only file can't be deleted until that's cleared.
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                if (attempt == DeleteAttempts)
                {
                    DeleteAtRestart(file);
                    return;
                }

                Thread.Sleep(RetryEvery);
            }
        }
    }

    private static void DeleteAtRestart(string path) =>
        NativeMethods.MoveFileEx(path, null, NativeMethods.MoveFileDelayUntilReboot);
}

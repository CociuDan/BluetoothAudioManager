using System.IO;
using BluetoothAudioManager.Installer.Interop;
using BluetoothAudioManager.Shared.Helpers;

namespace BluetoothAudioManager.Installer.Services;

/// <summary>
/// Removes everything the app installed or kept, for everyone on the PC. Bluetooth pairings and the sound output
/// belong to Windows, so they stay.
/// </summary>
internal static class AppUninstaller
{
    /// <param name="progress">Told the share done, from 0 to 1.</param>
    public static void Uninstall(IProgress<double> progress)
    {
        var installFolder = InstallLayout.InstallFolder;

        progress.Report(0.05);
        UpdaterService.Remove();
        RunningApp.CloseEverywhere(installFolder);

        progress.Report(0.15);
        UserCleanup.RemoveForEveryone();

        progress.Report(0.6);
        Shortcuts.Remove();
        using (var machine = InstallLayout.OpenMachineKey())
        {
            machine.DeleteSubKeyTree(InstallLayout.UninstallKeyPath, throwOnMissingSubKey: false);
            machine.DeleteSubKeyTree(InstallLayout.RegistryKeyPath, throwOnMissingSubKey: false);
        }

        progress.Report(0.7);
        InstalledFiles.DeleteFolder(InstallLayout.DataFolder);

        progress.Report(0.8);
        if (installFolder is not null)
        {
            InstalledFiles.Remove(installFolder);
        }

        progress.Report(1);
    }

    /// <summary>
    /// Has Windows delete this copy of the uninstaller when it next restarts, as it can't delete itself while running.
    /// Only a temporary copy, never the installer someone downloaded.
    /// </summary>
    public static void DeleteSelfLater()
    {
        var self = Environment.ProcessPath;
        if (self is not null
            && Path.GetFileName(self).StartsWith("BluetoothAudioManager-Uninstall-", StringComparison.OrdinalIgnoreCase)
            && string.Equals(Path.GetDirectoryName(self), Path.TrimEndingDirectorySeparator(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
        {
            NativeMethods.MoveFileEx(self, null, NativeMethods.MoveFileDelayUntilReboot);
        }
    }
}

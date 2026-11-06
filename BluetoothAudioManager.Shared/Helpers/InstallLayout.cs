using Microsoft.Win32;

namespace BluetoothAudioManager.Shared.Helpers;

/// <summary>
/// Where the installed app keeps its parts. The install folder, such as C:\Program Files\BluetoothAudioManager, holds
/// the app and the updater in folders of their own, and the uninstaller. Downloads and the updater's settings are in
/// C:\ProgramData\BluetoothAudioManager, which only administrators can change.
/// </summary>
public static class InstallLayout
{
    public const string ProductName = "Bluetooth Audio Manager";
    public const string DefaultFolderName = "BluetoothAudioManager";
    public const string AppFolderName = "app";
    public const string UpdaterFolderName = "updater";
    public const string AppExeName = "BluetoothAudioManager.App.exe";
    public const string AppProcessName = "BluetoothAudioManager.App";
    public const string UpdaterExeName = "BluetoothAudioManager.Updater.exe";
    public const string UninstallerName = "Uninstall.exe";
    public const string ServiceName = "BluetoothAudioManagerUpdater";
    public const string ServiceDisplayName = "Bluetooth Audio Manager Updater";
    public const string SettingsFileName = "updater.json";

    /// <summary>Starts the app hidden in the tray, as when starting with Windows.</summary>
    public const string TrayArgument = "--tray";

    /// <summary>The app's own key, under HKEY_LOCAL_MACHINE, with the install folder and version.</summary>
    public const string RegistryKeyPath = @"SOFTWARE\BluetoothAudioManager";

    /// <summary>Its entry in Installed apps, under HKEY_LOCAL_MACHINE.</summary>
    public const string UninstallKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\BluetoothAudioManager";

    public const string InstallFolderValue = "InstallFolder";
    public const string VersionValue = "Version";

    /// <summary>C:\ProgramData\BluetoothAudioManager: the updater's settings and downloads.</summary>
    public static string DataFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), DefaultFolderName);

    public static string SettingsPath => Path.Combine(DataFolder, SettingsFileName);

    /// <summary>The install folder, or null when the app isn't installed.</summary>
    public static string? InstallFolder => ReadValue(InstallFolderValue);

    /// <summary>The installed version, such as 0.0.3, or null when the app isn't installed.</summary>
    public static string? InstalledVersion => ReadValue(VersionValue);

    public static string AppFolder(string installFolder) => Path.Combine(installFolder, AppFolderName);

    public static string UpdaterFolder(string installFolder) => Path.Combine(installFolder, UpdaterFolderName);

    /// <summary>Whether this process is the installed app, rather than a build run from the code.</summary>
    public static bool IsInstalledApp() =>
        InstallFolder is { } folder
        && string.Equals(
            Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory),
            Path.TrimEndingDirectorySeparator(AppFolder(folder)),
            StringComparison.OrdinalIgnoreCase);

    /// <summary>HKEY_LOCAL_MACHINE, in its 64-bit view, where the app's keys are.</summary>
    public static RegistryKey OpenMachineKey() => RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);

    private static string? ReadValue(string name)
    {
        try
        {
            using var machine = OpenMachineKey();
            using var key = machine.OpenSubKey(RegistryKeyPath);
            return key?.GetValue(name) as string;
        }
        catch (Exception)
        {
            return null;
        }
    }
}

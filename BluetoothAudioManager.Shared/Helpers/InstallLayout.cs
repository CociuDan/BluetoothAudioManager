using Microsoft.Win32;

namespace BluetoothAudioManager.Shared.Helpers;

/// <summary>
/// Where the installed app keeps its parts. The install folder, such as C:\Program Files\BluetoothAudioManager, holds
/// the app and the updater in folders of their own, the uninstaller, and the latest version's installer in a hidden
/// folder. The updater's settings and log are in C:\ProgramData\BluetoothAudioManager. Only administrators can change either.
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

    /// <summary>
    /// Starts the app hidden in the tray as Windows starts, from the entry the installer adds for everyone, unless
    /// Start with the System is turned off in the app or in Task Manager.
    /// </summary>
    public const string StartupArgument = "--startup";

    /// <summary>Where Windows looks for programs to start at sign-in, under HKEY_LOCAL_MACHINE for everyone or HKEY_CURRENT_USER for one person.</summary>
    public const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>
    /// Where Task Manager's Startup apps tab, and Settings' Startup page, record a Run entry turned off or on: a binary
    /// value named like it, under HKEY_CURRENT_USER for one person or HKEY_LOCAL_MACHINE for everyone. Its first byte is
    /// odd when turned off. Windows doesn't document it, but it has worked this way since Windows 8.
    /// </summary>
    public const string StartupApprovedKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    /// <summary>The app's value in the Run keys.</summary>
    public const string RunValueName = "BluetoothAudioManager";

    /// <summary>
    /// Each person's own key, under HKEY_CURRENT_USER, where a test version kept their own choice of starting with
    /// Windows. That's for the whole PC now, so it's only removed.
    /// </summary>
    public const string UserKeyPath = @"SOFTWARE\BluetoothAudioManager";

    /// <summary>The release's one file: the installer, which the updater also downloads and runs to update.</summary>
    public const string SetupFileName = "BluetoothAudioManager-Setup.exe";

    /// <summary>The hidden folder in the install folder where the updater keeps the latest version's installer.</summary>
    public const string UpdateFolderName = ".update";

    /// <summary>Runs the installer without pages, to update the installed app, as the updater does.</summary>
    public const string UpdateArgument = "--update";

    /// <summary>
    /// Runs the downloaded installer in the background to unpack the new app into the hidden .update folder, as the
    /// updater does after each download, so updating only has to swap folders.
    /// </summary>
    public const string PrepareArgument = "--prepare";

    /// <summary>
    /// Runs the installer after the updater swapped in the prepared app and opened it: it replaces the updater, records
    /// the new version and starts the updater again, all while the app is already open.
    /// </summary>
    public const string FinishArgument = "--finish";

    /// <summary>Opens the app after an update, which skips asking the updater about updates as it starts.</summary>
    public const string UpdatedArgument = "--updated";

    /// <summary>The folder in .update where the next version's app is unpacked ahead of time.</summary>
    public const string StagedFolderName = "staged";

    /// <summary>The file in the staged folder, written last, naming the version unpacked there.</summary>
    public const string StagedVersionFileName = "version.txt";

    /// <summary>The name the replaced app folder has for a moment during an update, also left if one was interrupted.</summary>
    public const string OldFolderSuffix = ".old";

    /// <summary>Followed by a Windows session number: the session to open the app in after updating.</summary>
    public const string SessionOption = "--session";

    /// <summary>Opens the app after updating with its window showing, rather than in the tray.</summary>
    public const string WindowOption = "--window";

    /// <summary>A version that failed to install, so the updater doesn't offer it again.</summary>
    public const string FailedVersionValue = "FailedVersion";

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

    public static string UpdateFolder(string installFolder) => Path.Combine(installFolder, UpdateFolderName);

    public static string StagedFolder(string installFolder) => Path.Combine(UpdateFolder(installFolder), StagedFolderName);

    public static string StagedAppFolder(string installFolder) => Path.Combine(StagedFolder(installFolder), AppFolderName);

    /// <summary>The version unpacked and ready to swap in, or null if none is complete.</summary>
    public static string? StagedVersion(string installFolder)
    {
        try
        {
            var versionFile = Path.Combine(StagedFolder(installFolder), StagedVersionFileName);
            return File.Exists(versionFile) && Directory.Exists(StagedAppFolder(installFolder))
                ? File.ReadAllText(versionFile).Trim()
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>The version that last failed to install, or null if none has.</summary>
    public static string? FailedVersion => ReadValue(FailedVersionValue);

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

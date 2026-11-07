using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using BluetoothAudioManager.Shared.Helpers;
using Microsoft.Win32;

namespace BluetoothAudioManager.Installer.Services;

/// <summary>
/// Installs the app and the updater from the package carried inside the installer, adds the shortcuts and the
/// Installed apps entry, and starts the updater service. Installing again over an installed copy replaces it.
/// </summary>
internal static class AppInstaller
{
    private const string PackageResource = "AppPackage.zip";

    /// <summary>Whether this copy carries the package. Only the pipeline's builds do.</summary>
    public static bool HasPackage => Assembly.GetExecutingAssembly().GetManifestResourceInfo(PackageResource) is not null;

    /// <summary>The installer's version, which is the package's, such as 0.0.3.</summary>
    public static string Version => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

    /// <summary>The package carried inside the installer: the app and the updater, zipped.</summary>
    public static Stream OpenPackage() =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream(PackageResource)
        ?? throw new InvalidOperationException("This copy of the installer has nothing to install.");

    /// <param name="progress">Told the share done, from 0 to 1.</param>
    public static void Install(string installFolder, IProgress<double> progress)
    {
        installFolder = Path.GetFullPath(installFolder);

        // A copy already installed closes first, wherever it is, so its files can be replaced.
        progress.Report(0.02);
        UpdaterService.Remove();
        var previous = InstallLayout.InstallFolder;
        RunningApp.CloseEverywhere(previous);
        RunningApp.CloseEverywhere(installFolder);
        if (previous is not null
            && !string.Equals(Path.GetFullPath(previous), installFolder, StringComparison.OrdinalIgnoreCase))
        {
            InstalledFiles.Remove(previous);
        }

        // Emptied first, waiting a little for files a closing program hasn't let go of yet.
        InstalledFiles.DeleteFolder(InstallLayout.AppFolder(installFolder));
        InstalledFiles.DeleteFolder(InstallLayout.UpdaterFolder(installFolder));

        // Only administrators and Windows may change these, as the updater runs what's in them with administrator rights.
        // Program Files is already like that, but another folder chosen to install in may not be.
        SecureFolder.Ensure(InstallLayout.DataFolder);
        SecureFolder.Ensure(installFolder);

        // A downloaded installer from before starts afresh, as this install may be newer.
        InstalledFiles.DeleteFolder(InstallLayout.UpdateFolder(installFolder));

        progress.Report(0.05);
        using (var package = OpenPackage())
        using (var archive = new ZipArchive(package, ZipArchiveMode.Read))
        {
            PackageArchive.ExtractFolder(archive, InstallLayout.AppFolderName, InstallLayout.AppFolder(installFolder),
                share => progress.Report(0.05 + 0.6 * share));
            PackageArchive.ExtractFolder(archive, InstallLayout.UpdaterFolderName, InstallLayout.UpdaterFolder(installFolder),
                share => progress.Report(0.65 + 0.2 * share));
        }

        // This installer, copied in, is the uninstaller.
        File.Copy(Environment.ProcessPath!, Path.Combine(installFolder, InstallLayout.UninstallerName), overwrite: true);

        progress.Report(0.88);
        Register(installFolder);
        Shortcuts.Create(installFolder);

        progress.Report(0.92);
        UpdaterService.Install(installFolder);
        progress.Report(1);
    }

    /// <summary>
    /// Opens the installed app as the person signed in, without the installer's administrator rights,
    /// by asking Windows' shell to open it.
    /// </summary>
    public static void OpenApp(string installFolder)
    {
        try
        {
            var appExe = Path.Combine(InstallLayout.AppFolder(Path.GetFullPath(installFolder)), InstallLayout.AppExeName);
            Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), $"\"{appExe}\""))?.Dispose();
        }
        catch (Exception)
        {
            // It's in the Start menu, and opens with Windows if that's on.
        }
    }

    /// <summary>
    /// Writes the app's own key, read by the app and the updater, its entry in Installed apps, and its entry to start with
    /// Windows for everyone, for this version.
    /// </summary>
    public static void Register(string installFolder)
    {
        using var machine = InstallLayout.OpenMachineKey();
        using (var key = machine.CreateSubKey(InstallLayout.RegistryKeyPath))
        {
            key.SetValue(InstallLayout.InstallFolderValue, installFolder);
            key.SetValue(InstallLayout.VersionValue, Version);
            key.DeleteValue(InstallLayout.FailedVersionValue, throwOnMissingValue: false);
        }

        var appExe = Path.Combine(InstallLayout.AppFolder(installFolder), InstallLayout.AppExeName);
        var uninstaller = Path.Combine(installFolder, InstallLayout.UninstallerName);

        // Starts the app for everyone who signs in. Each person can still turn it off for themselves in the app.
        using (var run = machine.CreateSubKey(InstallLayout.RunKeyPath))
        {
            run.SetValue(InstallLayout.RunValueName, $"\"{appExe}\" {InstallLayout.StartupArgument}");
        }

        using var entry = machine.CreateSubKey(InstallLayout.UninstallKeyPath);
        entry.SetValue("DisplayName", InstallLayout.ProductName);
        entry.SetValue("DisplayVersion", Version);
        entry.SetValue("DisplayIcon", $"\"{appExe}\",0");
        entry.SetValue("InstallLocation", installFolder);
        entry.SetValue("UninstallString", $"\"{uninstaller}\" {App.UninstallArgument}");
        entry.SetValue("NoModify", 1, RegistryValueKind.DWord);
        entry.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        entry.SetValue("EstimatedSize", (int)(FolderSize(installFolder) / 1024), RegistryValueKind.DWord);
    }

    /// <summary>Notes that this version failed to install, so the updater doesn't offer it again.</summary>
    public static void RecordFailedVersion()
    {
        try
        {
            using var machine = InstallLayout.OpenMachineKey();
            using var key = machine.CreateSubKey(InstallLayout.RegistryKeyPath);
            key.SetValue(InstallLayout.FailedVersionValue, Version);
        }
        catch (Exception)
        {
            // Offered again then, which is no worse than before.
        }
    }

    private static long FolderSize(string folder) =>
        new DirectoryInfo(folder).EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
}

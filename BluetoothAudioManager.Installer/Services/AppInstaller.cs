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
    private static string Version => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

    /// <param name="progress">Told the share done, from 0 to 1.</param>
    public static void Install(string installFolder, IProgress<double> progress)
    {
        installFolder = Path.GetFullPath(installFolder);

        // A copy already installed closes first, wherever it is, so its files can be replaced.
        progress.Report(0.02);
        UpdaterService.Remove();
        RunningApp.CloseEverywhere();
        if (InstallLayout.InstallFolder is { } previous
            && !string.Equals(Path.GetFullPath(previous), installFolder, StringComparison.OrdinalIgnoreCase))
        {
            InstalledFiles.Remove(previous);
        }

        // The updater's folder starts empty and locked down, so nothing put there beforehand can be installed by it.
        SecureFolder.Ensure(InstallLayout.DataFolder);
        DeleteFolder(Path.Combine(InstallLayout.DataFolder, "downloads"));
        DeleteFolder(Path.Combine(InstallLayout.DataFolder, "staging"));

        progress.Report(0.05);
        Directory.CreateDirectory(installFolder);
        using (var package = Assembly.GetExecutingAssembly().GetManifestResourceStream(PackageResource)!)
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

    // The app's own key, read by the app and the updater, and its entry in Installed apps.
    private static void Register(string installFolder)
    {
        using var machine = InstallLayout.OpenMachineKey();
        using (var key = machine.CreateSubKey(InstallLayout.RegistryKeyPath))
        {
            key.SetValue(InstallLayout.InstallFolderValue, installFolder);
            key.SetValue(InstallLayout.VersionValue, Version);
        }

        var appExe = Path.Combine(InstallLayout.AppFolder(installFolder), InstallLayout.AppExeName);
        var uninstaller = Path.Combine(installFolder, InstallLayout.UninstallerName);

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

    private static long FolderSize(string folder) =>
        new DirectoryInfo(folder).EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);

    private static void DeleteFolder(string folder)
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}

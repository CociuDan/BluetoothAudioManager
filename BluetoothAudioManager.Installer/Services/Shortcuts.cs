using System.IO;
using System.Runtime.InteropServices;
using BluetoothAudioManager.Shared.Helpers;

namespace BluetoothAudioManager.Installer.Services;

/// <summary>The Start menu shortcuts, for everyone on the PC: the app, and its uninstaller.</summary>
internal static class Shortcuts
{
    private static string AppShortcut => Path.Combine(StartMenu, $"{InstallLayout.ProductName}.lnk");

    private static string UninstallShortcut => Path.Combine(StartMenu, $"Uninstall {InstallLayout.ProductName}.lnk");

    private static string StartMenu => Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);

    public static void Create(string installFolder)
    {
        var appExe = Path.Combine(InstallLayout.AppFolder(installFolder), InstallLayout.AppExeName);
        var uninstaller = Path.Combine(installFolder, InstallLayout.UninstallerName);

        Save(AppShortcut, appExe, "", appExe, "Shows your headphones' batteries and connects them.");
        Save(UninstallShortcut, uninstaller, App.UninstallArgument, appExe, $"Removes {InstallLayout.ProductName} from this PC.");
    }

    public static void Remove()
    {
        File.Delete(AppShortcut);
        File.Delete(UninstallShortcut);
    }

    // Windows Script Host's shell object writes .lnk files, so the installer needs no shortcut code of its own.
    private static void Save(string path, string target, string arguments, string icon, string description)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("Windows can't create shortcuts here.");
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic? link = null;

        try
        {
            link = shell.CreateShortcut(path);
            link.TargetPath = target;
            link.Arguments = arguments;
            link.WorkingDirectory = Path.GetDirectoryName(target);
            link.IconLocation = $"{icon},0";
            link.Description = description;
            link.Save();
        }
        finally
        {
            if (link is not null)
            {
                Marshal.FinalReleaseComObject(link);
            }

            Marshal.FinalReleaseComObject(shell);
        }
    }
}

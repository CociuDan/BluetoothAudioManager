using System.Diagnostics;
using System.IO;
using System.Windows;
using BluetoothAudioManager.Installer.Views;
using BluetoothAudioManager.Shared.Helpers;

namespace BluetoothAudioManager.Installer;

/// <summary>
/// Installs the app, or uninstalls it when started with --uninstall, as Installed apps and the Start menu shortcut do.
/// </summary>
public partial class App : Application
{
    public const string UninstallArgument = "--uninstall";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (!e.Args.Contains(UninstallArgument))
        {
            new MainWindow().Show();
            return;
        }

        // The uninstaller removes its own folder, which it can't while running from it, so it runs from a temporary copy.
        if (IsInInstallFolder())
        {
            var copy = Path.Combine(Path.GetTempPath(), $"BluetoothAudioManager-Uninstall-{Guid.NewGuid():N}.exe");
            File.Copy(Environment.ProcessPath!, copy);
            Process.Start(new ProcessStartInfo(copy, UninstallArgument) { UseShellExecute = false })?.Dispose();
            Shutdown();
            return;
        }

        new UninstallWindow().Show();
    }

    private static bool IsInInstallFolder() =>
        InstallLayout.InstallFolder is { } folder
        && string.Equals(
            Path.GetDirectoryName(Environment.ProcessPath),
            Path.TrimEndingDirectorySeparator(folder),
            StringComparison.OrdinalIgnoreCase);
}

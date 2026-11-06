using System.Diagnostics;
using System.IO;
using System.Windows;
using BluetoothAudioManager.Installer.Services;
using BluetoothAudioManager.Installer.Views;
using BluetoothAudioManager.Shared.Helpers;

namespace BluetoothAudioManager.Installer;

/// <summary>
/// Installs the app, uninstalls it when started with --uninstall, as Installed apps and the Start menu shortcut do,
/// or updates it without any pages when started with --update, as the updater service does.
/// </summary>
public partial class App : Application
{
    public const string UninstallArgument = "--uninstall";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Run by the updater service to update, without any pages.
        if (e.Args.Contains(InstallLayout.UpdateArgument))
        {
            SilentUpdate.Run(SessionFrom(e.Args), e.Args.Contains(InstallLayout.WindowOption));
            Shutdown();
            return;
        }

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

    // The number after --session, if there's a valid one.
    private static uint? SessionFrom(string[] args)
    {
        var index = Array.IndexOf(args, InstallLayout.SessionOption);
        return index >= 0 && index + 1 < args.Length && uint.TryParse(args[index + 1], out var session) ? session : null;
    }

    private static bool IsInInstallFolder() =>
        InstallLayout.InstallFolder is { } folder
        && string.Equals(
            Path.GetDirectoryName(Environment.ProcessPath),
            Path.TrimEndingDirectorySeparator(folder),
            StringComparison.OrdinalIgnoreCase);
}

using System.Diagnostics;
using System.IO;
using System.Windows;
using BluetoothAudioManager.Installer.Services;
using BluetoothAudioManager.Installer.Views;
using BluetoothAudioManager.Shared.Helpers;

namespace BluetoothAudioManager.Installer;

/// <summary>
/// Installs the app, uninstalls it when started with --uninstall, as Installed apps and the Start menu shortcut do,
/// or prepares and finishes updates without any pages, as the updater service asks.
/// </summary>
public partial class App : Application
{
    public const string UninstallArgument = "--uninstall";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Run by the updater service, without any pages: to unpack a download ahead of time, to finish an update
        // after it swapped in the new app, or to do the whole update.
        if (e.Args.Contains(InstallLayout.PrepareArgument))
        {
            UpdatePreparation.Run();
            Shutdown();
            return;
        }

        if (e.Args.Contains(InstallLayout.FinishArgument))
        {
            SilentUpdate.Finish();
            Shutdown();
            return;
        }

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

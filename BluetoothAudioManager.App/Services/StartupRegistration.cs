using System.IO;
using System.Reflection;
using System.Security;
using BluetoothAudioManager.Shared.Helpers;
using Microsoft.Win32;

namespace BluetoothAudioManager.App.Services;

/// <summary>
/// Starting with the system. The installed app starts for everyone from the entry its installer adds, and its switch is
/// the same on or off as Task Manager's Startup apps tab shows, for the whole PC. Changing that needs administrator
/// rights, so the app asks the updater service. A build that wasn't installed has no such entry, so its switch adds
/// or removes one for the current person instead.
/// </summary>
internal static class StartupRegistration
{
    public const string TrayArgument = InstallLayout.TrayArgument;
    public const string StartupArgument = InstallLayout.StartupArgument;

    private static readonly bool IsInstalled = InstallLayout.IsInstalledApp();
    private static readonly TimeSpan UpdaterTimeout = TimeSpan.FromSeconds(3);

    public static bool IsEnabled =>
        IsInstalled
            ? !IsTurnedOff(Registry.LocalMachine) && !IsTurnedOff(Registry.CurrentUser)
            : HasOwnEntry && !IsTurnedOff(Registry.CurrentUser);

    /// <summary>Turns starting with the system on or off, and returns whether it's on afterwards.</summary>
    public static async Task<bool> SetEnabledAsync(bool enabled)
    {
        if (IsInstalled)
        {
            await UpdaterPipe.SendAsync(
                $"{UpdaterPipe.StartupCommand} {(enabled ? UpdaterPipe.OnOption : UpdaterPipe.OffOption)}", UpdaterTimeout);
        }
        else
        {
            using var run = Registry.CurrentUser.CreateSubKey(InstallLayout.RunKeyPath);
            if (enabled)
            {
                run.SetValue(InstallLayout.RunValueName, $"\"{GetExecutablePath()}\" {TrayArgument}");
            }
            else
            {
                run.DeleteValue(InstallLayout.RunValueName, false);
            }
        }

        // Settings' Startup page may have turned it off for this person only, which the app can undo itself.
        if (enabled)
        {
            ClearTurnedOff(Registry.CurrentUser);
        }

        return IsEnabled;
    }

    /// <summary>
    /// Removes what earlier versions kept for this person: their own startup entry, which would start the installed app
    /// a second time beside the entry for everyone, and their own on or off choice, as it's for the whole PC now.
    /// </summary>
    public static void RemoveOldPersonalSettingsIfInstalled()
    {
        if (!IsInstalled)
        {
            return;
        }

        using (var run = Registry.CurrentUser.OpenSubKey(InstallLayout.RunKeyPath, writable: true))
        {
            run?.DeleteValue(InstallLayout.RunValueName, false);
        }

        Registry.CurrentUser.DeleteSubKeyTree(InstallLayout.UserKeyPath, throwOnMissingSubKey: false);
    }

    private static bool HasOwnEntry
    {
        get
        {
            using var run = Registry.CurrentUser.OpenSubKey(InstallLayout.RunKeyPath);
            return run?.GetValue(InstallLayout.RunValueName) is not null;
        }
    }

    private static bool IsTurnedOff(RegistryKey root)
    {
        try
        {
            using var key = root.OpenSubKey(InstallLayout.StartupApprovedKeyPath);
            return StartupApproval.IsOff(key?.GetValue(InstallLayout.RunValueName));
        }
        catch (Exception exception) when (exception is SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    // Without a value, Windows treats the entry as on.
    private static void ClearTurnedOff(RegistryKey root)
    {
        try
        {
            using var key = root.OpenSubKey(InstallLayout.StartupApprovedKeyPath, writable: true);
            key?.DeleteValue(InstallLayout.RunValueName, throwOnMissingValue: false);
        }
        catch (Exception exception) when (exception is SecurityException or UnauthorizedAccessException or IOException)
        {
        }
    }

    // Built from the app's own folder because a debug run is hosted by dotnet.exe.
    private static string GetExecutablePath() =>
        Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetEntryAssembly()!.GetName().Name}.exe");
}

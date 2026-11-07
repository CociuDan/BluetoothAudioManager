using System.IO;
using System.Reflection;
using System.Security;
using BluetoothAudioManager.Shared.Helpers;
using Microsoft.Win32;

namespace BluetoothAudioManager.App.Services;

/// <summary>
/// Starting with Windows. The installed app starts for everyone from the entry its installer adds, and this is each
/// person's choice whether it should, on unless they turn it off. A build that wasn't installed has no such entry,
/// so it adds one for the current person instead. Either way, turning the app off in Task Manager's Startup apps tab
/// counts as off here too, and turning it on here undoes that.
/// </summary>
internal static class StartupRegistration
{
    public const string TrayArgument = InstallLayout.TrayArgument;
    public const string StartupArgument = InstallLayout.StartupArgument;

    private static readonly bool IsInstalled = InstallLayout.IsInstalledApp();

    public static bool IsEnabled => IsOwnSettingOn && !IsTurnedOffInWindows;

    /// <summary>Turns starting with Windows on or off for this person. Returns whether it's on afterwards.</summary>
    public static bool SetEnabled(bool enabled)
    {
        if (IsInstalled)
        {
            using var key = Registry.CurrentUser.CreateSubKey(InstallLayout.UserKeyPath);
            key.SetValue(InstallLayout.StartWithWindowsValue, enabled ? 1 : 0, RegistryValueKind.DWord);
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

        // Turned off here, it stays as Windows has it, as the app closes itself when started anyway.
        if (enabled)
        {
            ClearTurnedOff(Registry.CurrentUser);
            ClearTurnedOff(Registry.LocalMachine);
        }

        return IsEnabled;
    }

    /// <summary>
    /// Removes the entry an earlier version added for this person, as the installer's entry for everyone starts the
    /// installed app now, and both would start it twice.
    /// </summary>
    public static void RemoveOwnEntryIfInstalled()
    {
        if (!IsInstalled)
        {
            return;
        }

        using var run = Registry.CurrentUser.OpenSubKey(InstallLayout.RunKeyPath, writable: true);
        run?.DeleteValue(InstallLayout.RunValueName, false);
    }

    private static bool IsOwnSettingOn
    {
        get
        {
            if (IsInstalled)
            {
                using var key = Registry.CurrentUser.OpenSubKey(InstallLayout.UserKeyPath);
                return key?.GetValue(InstallLayout.StartWithWindowsValue) is not int on || on != 0;
            }

            using var run = Registry.CurrentUser.OpenSubKey(InstallLayout.RunKeyPath);
            return run?.GetValue(InstallLayout.RunValueName) is not null;
        }
    }

    // Task Manager's Startup apps tab, or Settings' Startup page, turned the app off for this person or for everyone.
    private static bool IsTurnedOffInWindows => IsTurnedOff(Registry.CurrentUser) || IsTurnedOff(Registry.LocalMachine);

    private static bool IsTurnedOff(RegistryKey root)
    {
        try
        {
            using var key = root.OpenSubKey(InstallLayout.StartupApprovedKeyPath);
            return key?.GetValue(InstallLayout.RunValueName) is byte[] { Length: > 0 } state && (state[0] & 1) == 1;
        }
        catch (Exception exception) when (exception is SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    // Without a value, Windows treats the entry as on. Turned off for everyone, it needs administrator rights to undo,
    // so then it stays off, and the switch shows that.
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

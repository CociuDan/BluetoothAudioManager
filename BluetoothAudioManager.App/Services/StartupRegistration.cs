using System.IO;
using System.Reflection;
using BluetoothAudioManager.Shared.Helpers;
using Microsoft.Win32;

namespace BluetoothAudioManager.App.Services;

/// <summary>Turns starting with Windows on or off for the current user.</summary>
internal static class StartupRegistration
{
    public const string TrayArgument = InstallLayout.TrayArgument;

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "BluetoothAudioManager";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is not null;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);

        if (enabled)
        {
            key.SetValue(ValueName, $"\"{GetExecutablePath()}\" {TrayArgument}");
        }
        else
        {
            key.DeleteValue(ValueName, false);
        }
    }

    // Built from the app's own folder because a debug run is hosted by dotnet.exe.
    private static string GetExecutablePath() =>
        Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetEntryAssembly()!.GetName().Name}.exe");
}

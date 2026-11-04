using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace BluetoothAudioManager.App;

/// <summary>Matches the title bar to the Windows app theme, which the Fluent theme doesn't do on Windows 10.</summary>
internal static class TitleBarTheme
{
    private const int UseImmersiveDarkMode = 20;
    private const string PersonalizeKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static void Follow(Window window)
    {
        UserPreferenceChangedEventHandler onChanged = (_, e) =>
        {
            if (e.Category == UserPreferenceCategory.General)
            {
                window.Dispatcher.BeginInvoke(() => Apply(window));
            }
        };

        window.SourceInitialized += (_, _) => Apply(window);
        SystemEvents.UserPreferenceChanged += onChanged;
        window.Closed += (_, _) => SystemEvents.UserPreferenceChanged -= onChanged;
    }

    private static void Apply(Window window)
    {
        // The value is missing on Windows versions without a theme setting, which are light.
        var appsUseLightTheme = Registry.GetValue(PersonalizeKey, "AppsUseLightTheme", 1) as int? ?? 1;
        var useDark = appsUseLightTheme == 0 ? 1 : 0;
        var handle = new WindowInteropHelper(window).Handle;

        DwmSetWindowAttribute(handle, UseImmersiveDarkMode, ref useDark, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int valueSize);
}

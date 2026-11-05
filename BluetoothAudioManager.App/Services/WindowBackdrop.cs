using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using BluetoothAudioManager.App.Interop;

namespace BluetoothAudioManager.App.Services;

/// <summary>Gives a borderless window a blurred, see-through background and rounded corners that follow the Windows app theme.</summary>
internal static class WindowBackdrop
{
    private const double CornerRadius = 8;
    private const int AccentEnableBlurBehind = 3;
    private const int WindowCompositionAccentPolicy = 19;
    private const string PersonalizeKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private static readonly Brush DarkSurface = new SolidColorBrush(Color.FromArgb(0xB8, 0x20, 0x20, 0x20));
    private static readonly Brush DarkEdge = new SolidColorBrush(Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF));
    private static readonly Brush LightSurface = new SolidColorBrush(Color.FromArgb(0xC7, 0xF3, 0xF3, 0xF3));
    private static readonly Brush LightEdge = new SolidColorBrush(Color.FromArgb(0x24, 0x00, 0x00, 0x00));

    public static void Follow(Window window, Border surface)
    {
        UserPreferenceChangedEventHandler onChanged = (_, e) =>
        {
            if (e.Category == UserPreferenceCategory.General)
            {
                window.Dispatcher.BeginInvoke(() => ApplyTheme(surface));
            }
        };

        window.SourceInitialized += (_, _) =>
        {
            EnableBlur(new WindowInteropHelper(window).Handle);
            ApplyTheme(surface);
            RoundCorners(window);
        };
        window.SizeChanged += (_, _) => RoundCorners(window);
        window.DpiChanged += (_, _) => RoundCorners(window);
        SystemEvents.UserPreferenceChanged += onChanged;
        window.Closed += (_, _) => SystemEvents.UserPreferenceChanged -= onChanged;
    }

    private static void ApplyTheme(Border surface)
    {
        // The value is missing on Windows versions without a theme setting, which are light.
        var isLight = (Registry.GetValue(PersonalizeKey, "AppsUseLightTheme", 1) as int? ?? 1) != 0;

        surface.Background = isLight ? LightSurface : DarkSurface;
        surface.BorderBrush = isLight ? LightEdge : DarkEdge;
    }

    private static void EnableBlur(IntPtr handle)
    {
        var size = Marshal.SizeOf<AccentPolicy>();
        var pointer = Marshal.AllocHGlobal(size);

        try
        {
            Marshal.StructureToPtr(new AccentPolicy { AccentState = AccentEnableBlurBehind }, pointer, false);
            var data = new WindowCompositionAttributeData
            {
                Attribute = WindowCompositionAccentPolicy,
                Data = pointer,
                SizeOfData = size,
            };
            SetWindowCompositionAttribute(handle, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    // The blur fills the whole window rectangle, so the window itself has to be cut to the rounded shape.
    private static void RoundCorners(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero || !GetWindowRect(handle, out var bounds))
        {
            return;
        }

        var diameter = (int)Math.Round(CornerRadius * 2 * VisualTreeHelper.GetDpi(window).DpiScaleX);
        var region = CreateRoundRectRgn(0, 0, bounds.Right - bounds.Left + 1, bounds.Bottom - bounds.Top + 1, diameter, diameter);

        // Windows owns the region after this call, so it isn't deleted here.
        SetWindowRgn(handle, region, true);
    }

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr window, ref WindowCompositionAttributeData data);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect bounds);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr window, IntPtr region, [MarshalAs(UnmanagedType.Bool)] bool redraw);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int widthEllipse, int heightEllipse);
}

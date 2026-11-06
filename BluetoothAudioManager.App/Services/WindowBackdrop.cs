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

    // The least a color may be, out of 255, so a fully see-through window still catches clicks:
    // Windows passes clicks on completely invisible parts of a window through to what's behind it.
    private const byte LeastAlpha = 1;
    private const int WindowCompositionAccentPolicy = 19;
    private const string PersonalizeKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>How see-through the window is by default, in percent, close to how it always looked in both themes.</summary>
    public const int DefaultTransparency = 25;

    /// <summary>The most see-through allowed, in percent, so the window is never so clear its contents seem to float.</summary>
    // Raised to 100 for now, to try out where the limit should be.
    public const int MaxTransparency = 100;

    private const int TransparencyStep = 5;

    // The card lifted while a device is dragged is a little more see-through than the window: 60% solid at the default.
    private const double CardShare = 0.8;

    private static readonly Color DarkColor = Color.FromRgb(0x20, 0x20, 0x20);
    private static readonly Color LightColor = Color.FromRgb(0xF3, 0xF3, 0xF3);
    private static readonly Brush DarkEdge = new SolidColorBrush(Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF));
    private static readonly Brush LightEdge = new SolidColorBrush(Color.FromArgb(0x24, 0x00, 0x00, 0x00));

    private static Border? _surface;
    private static int _transparency = DefaultTransparency;

    /// <summary>Whether the window follows the light app theme, as last applied.</summary>
    public static bool IsLight { get; private set; } = true;

    /// <summary>The window's thin edge, for anything drawn to look like part of the window.</summary>
    public static Brush Edge => IsLight ? LightEdge : DarkEdge;

    /// <summary>The background of the card lifted while a device is dragged, following the window's transparency.</summary>
    public static Brush CardBackground => Surface(SolidShare * CardShare);

    /// <summary>
    /// How see-through the window's color is, in percent, kept from solid to the most allowed. Applied straight away.
    /// Windows' blur behind the window always stays.
    /// </summary>
    public static int Transparency
    {
        get => _transparency;
        set
        {
            _transparency = ClampTransparency(value);

            if (_surface is not null)
            {
                ApplyTheme(_surface);
            }
        }
    }

    /// <summary>Keeps a transparency from solid to the most allowed, in steps of 5%.</summary>
    public static int ClampTransparency(int percent) =>
        Math.Clamp((int)Math.Round(percent / (double)TransparencyStep) * TransparencyStep, 0, MaxTransparency);

    public static void Follow(Window window, Border surface)
    {
        _surface = surface;

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

    // How solid the window's color is, from 0 to 1.
    private static double SolidShare => (100 - _transparency) / 100.0;
    private static void ApplyTheme(Border surface)
    {
        // The value is missing on Windows versions without a theme setting, which are light.
        IsLight = (Registry.GetValue(PersonalizeKey, "AppsUseLightTheme", 1) as int? ?? 1) != 0;

        surface.Background = Surface(SolidShare);
        surface.BorderBrush = Edge;
    }

    // The theme's window color, as solid as the share, from 0 to 1.
    private static Brush Surface(double solidShare)
    {
        var color = IsLight ? LightColor : DarkColor;
        color.A = Math.Max(LeastAlpha, (byte)Math.Round(255 * solidShare));
        return new SolidColorBrush(color);
    }

    // Windows' blur behind the window can only be on or off: its strength can't be changed.
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

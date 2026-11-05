using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BluetoothAudioManager.Core.Enums;
using BluetoothAudioManager.Core.Helpers;
using BluetoothAudioManager.Core.Models;

namespace BluetoothAudioManager.App.Views;

public partial class BatteryBadge : UserControl
{
    private static readonly Brush GoodBrush = new SolidColorBrush(Color.FromRgb(0x15, 0x80, 0x3D));
    private static readonly Brush WarningBrush = new SolidColorBrush(Color.FromRgb(0xEA, 0xB3, 0x08));
    private static readonly Brush LowBrush = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
    private static readonly Brush LightText = Brushes.White;
    private static readonly Brush DarkText = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));

    public BatteryBadge()
    {
        InitializeComponent();
    }

    /// <summary>Text shown before the level, for when one picture covers several batteries.</summary>
    public string? Label { get; set; }

    public void Show(TrackedBattery battery, DateTimeOffset now)
    {
        if (battery.PercentAt(now) is not { } percent)
        {
            Clear();
            return;
        }

        var band = BatteryBands.FromPercent(percent);
        var text = band == BatteryBand.Warning ? DarkText : LightText;

        Badge.Background = band switch
        {
            BatteryBand.Good => GoodBrush,
            BatteryBand.Warning => WarningBrush,
            _ => LowBrush,
        };
        PercentText.Foreground = text;
        PercentText.Text = Label is null ? $"{percent}%" : $"{Label} {percent}%";
        ChargingIcon.Visibility = battery.IsChargingAt(now) ? Visibility.Visible : Visibility.Collapsed;
        BadgeArea.Visibility = Visibility.Visible;
    }

    public void Clear()
    {
        BadgeArea.Visibility = Visibility.Collapsed;
    }
}

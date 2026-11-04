using System.Windows.Controls;
using System.Windows.Media;
using BluetoothAudioManager.Core;

namespace BluetoothAudioManager.App;

public partial class BatteryView : UserControl
{
    private const double FullWidth = 21;
    private const double MinimumVisibleWidth = 2;

    private static readonly Brush GoodBrush = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));
    private static readonly Brush WarningBrush = new SolidColorBrush(Color.FromRgb(0xEA, 0xB3, 0x08));
    private static readonly Brush LowBrush = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));

    public BatteryView()
    {
        InitializeComponent();
    }

    public void Show(TrackedBattery battery, DateTimeOffset now)
    {
        DetailText.Text = battery.DescribeDetail(now);

        if (battery.Percent is not { } percent)
        {
            ShowEmptyLevel();
            return;
        }

        LevelFill.Width = Math.Max(MinimumVisibleWidth, FullWidth * percent / 100);
        LevelFill.Fill = BatteryBands.FromPercent(percent) switch
        {
            BatteryBand.Good => GoodBrush,
            BatteryBand.Warning => WarningBrush,
            _ => LowBrush,
        };
        PercentText.Text = $"{percent}%";
    }

    public void Clear()
    {
        DetailText.Text = "";
        ShowEmptyLevel();
    }

    private void ShowEmptyLevel()
    {
        LevelFill.Width = 0;
        PercentText.Text = "";
    }
}

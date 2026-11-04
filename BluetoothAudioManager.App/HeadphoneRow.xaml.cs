using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using BluetoothAudioManager.Core;

namespace BluetoothAudioManager.App;

/// <summary>
/// A paired audio device with no special support: its picture and connection buttons.
/// A battery badge and settings appear only if Windows reports a level for it.
/// </summary>
public partial class HeadphoneRow : UserControl, IDisposable
{
    private readonly DeviceConnectionMonitor _connection;
    private readonly AppSettings _settings;
    private readonly LowBatteryWatcher _lowBatteryWatcher = new();
    private readonly DispatcherTimer _batteryTimer = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly string _title;
    private bool _reportsBattery;

    public HeadphoneRow(string deviceName, string title, ImageSource picture, AppSettings settings)
    {
        InitializeComponent();

        DeviceName = deviceName;
        _title = title;
        _settings = settings;
        _connection = new DeviceConnectionMonitor(deviceName);
        Header.Title = title;
        Picture.Source = picture;

        var saved = SavedLowBattery;
        BatterySetting.Show(saved.Enabled, saved.Level);
        BatterySetting.Changed += OnLowBatteryChanged;
        Header.SettingsClicked += () => ShowSettings(BatterySetting.Visibility != Visibility.Visible);
        Header.StateChanged += _ => RefreshBattery();
        _batteryTimer.Tick += (_, _) => RefreshBattery();

        Header.Follow(_connection);
        _batteryTimer.Start();
    }

    /// <summary>Raised with the title and message to show when the battery runs low.</summary>
    public event Action<string, string>? LowBatteryAlert;

    /// <summary>Raised when the row changes height, because the device turned out to report a battery.</summary>
    public event Action? HeightChanged;

    public string DeviceName { get; }

    public bool IsConnected => Header.State == DeviceConnectionState.Connected;

    public bool ShowDivider
    {
        set => Divider.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    private DeviceLowBattery SavedLowBattery =>
        _settings.DeviceLowBattery.GetValueOrDefault(DeviceName) ?? new DeviceLowBattery();

    public void RefreshOutputState() => Header.RefreshOutputState();

    public void ShowSettings(bool open)
    {
        BatterySetting.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        PictureArea.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
        Header.SettingsOpen = open;
    }

    public void Dispose()
    {
        _batteryTimer.Stop();
        Header.Stop();
        _connection.Dispose();
    }

    private void OnLowBatteryChanged(bool enabled, int level)
    {
        _settings.DeviceLowBattery[DeviceName] = new DeviceLowBattery { Enabled = enabled, Level = level };
        _settings.Save();
        _lowBatteryWatcher.Reset();
    }

    private async void RefreshBattery()
    {
        int? percent = null;

        if (IsConnected)
        {
            try
            {
                percent = await DeviceBatteryReader.ReadPercentAsync(DeviceName);
            }
            catch (Exception)
            {
                // Treated the same as a device that doesn't report a level.
            }
        }

        if (percent is null)
        {
            Badge.Clear();
            return;
        }

        if (!_reportsBattery)
        {
            _reportsBattery = true;
            Header.ShowSettings = true;
            Badge.Visibility = Visibility.Visible;
            HeightChanged?.Invoke();
        }

        var reading = new BatteryReading(percent, false);
        var now = DateTimeOffset.Now;
        Badge.Show(new TrackedBattery(percent, false, now), now);

        var saved = SavedLowBattery;
        if (saved.Enabled && _lowBatteryWatcher.UpdatePart(_title, reading, saved.Level) is { } message)
        {
            LowBatteryAlert?.Invoke($"{_title} battery low", message);
        }
    }
}

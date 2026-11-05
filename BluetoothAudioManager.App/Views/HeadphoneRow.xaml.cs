using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using BluetoothAudioManager.App.Models;
using BluetoothAudioManager.Core.Enums;
using BluetoothAudioManager.Core.Interfaces;
using BluetoothAudioManager.Core.Models;
using BluetoothAudioManager.Core.Services;

namespace BluetoothAudioManager.App.Views;

/// <summary>
/// An audio device other than AirPods with pods: its picture and connection buttons.
/// A battery badge and settings appear only if Windows reports a level for it.
/// </summary>
public partial class HeadphoneRow : UserControl, IDisposable
{
    private readonly IAudioDeviceConnection _connection;
    private readonly AppSettings _settings;
    private readonly LowBatteryWatcher _lowBatteryWatcher = new();
    private readonly DispatcherTimer _batteryTimer = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly Func<Task<BatteryReading?>> _readBattery;
    private string _title;
    private bool _reportsBattery;

    /// <param name="connection">Follows the device. The row disposes of it.</param>
    /// <param name="readBattery">Reads the device's battery level and whether it's charging. A null reading or level means it doesn't report one.</param>
    public HeadphoneRow(
        string deviceName,
        string title,
        ImageSource picture,
        AppSettings settings,
        IAudioDeviceConnection connection,
        Func<Task<BatteryReading?>> readBattery)
    {
        InitializeComponent();

        DeviceName = deviceName;
        _title = title;
        _settings = settings;
        _connection = connection;
        _readBattery = readBattery;
        Header.Title = title;
        Picture.Source = picture;

        var saved = SavedLowBattery;
        BatterySetting.Show(saved.Enabled, saved.Level);
        BatterySetting.Changed += OnLowBatteryChanged;
        Header.SettingsClicked += () => ShowSettings(BatterySetting.Visibility != Visibility.Visible);
        Header.StateChanged += OnStateChanged;
        Header.DragStarted += () => DragStarted?.Invoke();
        _batteryTimer.Tick += (_, _) => RefreshBattery();

        Header.Follow(_connection);
        _batteryTimer.Start();
    }

    /// <summary>Raised with the title and message to show when the battery runs low.</summary>
    public event Action<string, string>? LowBatteryAlert;

    /// <summary>Raised when the row changes height, because the device turned out to report a battery.</summary>
    public event Action? HeightChanged;

    /// <summary>Raised when the user presses on the title, to start dragging the row to another place.</summary>
    public event Action? DragStarted;

    public string DeviceName { get; }

    public bool IsConnected => Header.State == DeviceConnectionState.Connected;

    public bool ShowDivider
    {
        set => Divider.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>The title shown and used in alerts, for when a better name than the Windows one becomes known.</summary>
    public string Title
    {
        set
        {
            _title = value;
            Header.Title = value;
        }
    }

    private DeviceLowBattery SavedLowBattery =>
        _settings.DeviceLowBattery.GetValueOrDefault(DeviceName) ?? new DeviceLowBattery();

    public void RefreshOutputState() => Header.RefreshOutputState();

    /// <summary>Checks the connection now, rather than waiting for the next regular check.</summary>
    public void RefreshConnection() => Header.CheckNow();

    public void ShowSettings(bool open)
    {
        BatterySetting.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        PictureArea.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
        Header.SettingsOpen = open;
    }

    /// <summary>Reads the battery now, rather than waiting for the next regular check.</summary>
    public async void RefreshBattery()
    {
        BatteryReading? reading = null;

        if (IsConnected)
        {
            try
            {
                reading = await _readBattery();
            }
            catch (Exception)
            {
                // Treated the same as a device that doesn't report a level.
            }
        }

        if (reading?.Percent is not { } percent)
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

        var now = DateTimeOffset.Now;
        Badge.Show(new TrackedBattery(percent, reading.IsCharging, now), now);

        var saved = SavedLowBattery;
        if (saved.Enabled && _lowBatteryWatcher.UpdatePart(_title, reading, saved.Level) is { } message)
        {
            LowBatteryAlert?.Invoke($"{_title} battery low", message);
        }
    }

    private void OnStateChanged(DeviceConnectionState state)
    {
        // Settings can only be opened while connected, so they close when the device disconnects.
        if (state != DeviceConnectionState.Connected)
        {
            ShowSettings(false);
        }

        RefreshBattery();
    }

    private void OnLowBatteryChanged(bool enabled, int level)
    {
        _settings.DeviceLowBattery[DeviceName] = new DeviceLowBattery { Enabled = enabled, Level = level };
        _settings.Save();
        _lowBatteryWatcher.Reset();
    }

    public void Dispose()
    {
        _batteryTimer.Stop();
        Header.Stop();
        _connection.Dispose();
    }
}

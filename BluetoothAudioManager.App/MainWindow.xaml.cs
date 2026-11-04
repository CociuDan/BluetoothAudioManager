using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using BluetoothAudioManager.Core;

namespace BluetoothAudioManager.App;

public partial class MainWindow : Window
{
    private static readonly TimeSpan RetryEvery = TimeSpan.FromSeconds(5);

    // Clicking the tray icon first takes focus from the window, hiding it, so this stops that click reopening it.
    private static readonly TimeSpan ClickAwayGrace = TimeSpan.FromMilliseconds(500);
    private const double CornerGap = 12;
    private const double TitleBarHeight = 32;
    private const double WindowEdges = 2;
    private const double NoDevicesHeight = 240;
    private const int MaximumRowsWithoutScrolling = 2;

    private readonly AirPodsMonitor _monitor = new();
    private readonly DeviceConnectionMonitor _airPodsConnection = new(PairedAudioDevices.AirPodsName);
    private readonly List<HeadphoneRow> _otherRows = [];
    private readonly BatteryTracker _tracker = new();
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly EarDetector _earDetector = new();
    private readonly LowBatteryWatcher _lowBatteryWatcher = new();
    private DefaultOutputWatcher? _outputWatcher;
    private readonly DispatcherTimer _earDetectionTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly SemaphoreSlim _earActionGate = new(1, 1);
    private (bool Left, bool Right)? _lastInEar;
    private IReadOnlyList<string> _pausedApps = [];
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private string? _scanError;
    private DateTimeOffset _lastScanAttempt;
    private DateTimeOffset _lastDeviceCheck;
    private bool _refreshingDevices;
    private bool _devicesShownOnce;
    private DateTimeOffset _lastHiddenByClickAway;
    private bool _closeAllowed;

    private bool CanShowBattery =>
        AirPodsHeader.State is not (null or DeviceConnectionState.Disconnected);

    /// <summary>Raised with the message to show when a battery runs low.</summary>
    public event Action<string>? LowBatteryAlert;

    public MainWindow()
    {
        InitializeComponent();
        WindowBackdrop.Follow(this, Surface);

        _monitor.StatusReceived += status => Dispatcher.BeginInvoke(() => OnStatusReceived(status));
        _monitor.ScanFailed += error => Dispatcher.BeginInvoke(() => OnScanFailed($"Bluetooth scanning stopped: {error}. Retrying."));
        AirPodsHeader.StateChanged += OnAirPodsStateChanged;
        _timer.Tick += (_, _) => OnTick();

        // Sized again once the theme has styled everything, because sizes measured before that can be too small.
        Loaded += (_, _) =>
        {
            SizeToDevices();
            PositionInCorner();
        };

        AirPodsHeader.SettingsClicked += ToggleSettings;

        // Closed on hiding, so the window always reopens showing the batteries.
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is false)
            {
                ShowSettings(false);
            }
        };
        ShowEarDetectionSetting();
        LowBatteryLevelSlider.Value = _settings.LowBatteryLevel;
        ShowLowBatterySetting();

        // Added after the saved level is shown, so showing it doesn't count as a change.
        LowBatteryLevelSlider.ValueChanged += (_, e) => OnLowBatteryLevelChanged((int)e.NewValue);

        Deactivated += (_, _) => OnClickedAway();
        Closing += (_, e) =>
        {
            if (!_closeAllowed)
            {
                e.Cancel = true;
                Hide();
            }
        };
        Closed += (_, _) =>
        {
            _timer.Stop();
            _earDetectionTimer.Stop();
            AirPodsHeader.Stop();
            _monitor.Dispose();
            _airPodsConnection.Dispose();
            _otherRows.ForEach(row => row.Dispose());
            _outputWatcher?.Dispose();
        };
    }

    /// <summary>Starts following the devices, whether or not the window is showing.</summary>
    public void StartMonitoring()
    {
        StartScanning();
        AirPodsHeader.Follow(_airPodsConnection);
        RefreshDevices();
        WatchOutput();
        _timer.Start();
        _earDetectionTimer.Tick += (_, _) => RunEarDetection();
        _earDetectionTimer.Start();
    }

    private async void RefreshDevices()
    {
        if (_refreshingDevices)
        {
            return;
        }

        _refreshingDevices = true;
        _lastDeviceCheck = DateTimeOffset.Now;

        try
        {
            ShowDevices(await PairedAudioDevices.FindNamesAsync());
        }
        catch (Exception)
        {
            // The list stays as it is until the next check.
        }
        finally
        {
            _refreshingDevices = false;
        }
    }

    private void ShowDevices(IReadOnlyList<string> names)
    {
        var otherNames = names.Where(name => !PairedAudioDevices.IsAirPods(name)).ToList();

        foreach (var row in _otherRows.Where(row => !otherNames.Contains(row.DeviceName)).ToList())
        {
            DevicesPanel.Children.Remove(row);
            _otherRows.Remove(row);
            row.Dispose();
        }

        foreach (var name in otherNames.Where(name => _otherRows.All(row => row.DeviceName != name)))
        {
            var row = new HeadphoneRow(name, TitleFor(name), PictureFor(name));
            _otherRows.Add(row);
            DevicesPanel.Children.Add(row);
        }

        AirPodsSection.Visibility = names.Count > otherNames.Count ? Visibility.Visible : Visibility.Collapsed;
        NoDevicesPanel.Visibility = names.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ShowDividers();

        if (!_devicesShownOnce)
        {
            _devicesShownOnce = true;
            SizeToDevices();
            PositionInCorner();
        }
    }

    private static string TitleFor(string deviceName) =>
        IsMajorIV(deviceName) ? "Marshall Major IV" : deviceName;

    private ImageSource PictureFor(string deviceName) =>
        (ImageSource)FindResource(IsMajorIV(deviceName) ? "HeadphonesImage" : "GenericHeadphonesImage");

    private static bool IsMajorIV(string deviceName) =>
        deviceName.Contains("MAJOR IV", StringComparison.OrdinalIgnoreCase);

    // Only done as the window opens, so rows don't move while it's being used.
    private void ArrangeRows()
    {
        var rows = DevicesPanel.Children.Cast<UIElement>().OrderByDescending(IsConnected).ToList();

        DevicesPanel.Children.Clear();
        rows.ForEach(row => DevicesPanel.Children.Add(row));
        ShowDividers();
    }

    private bool IsConnected(UIElement row) =>
        row is HeadphoneRow headphones
            ? headphones.IsConnected
            : AirPodsHeader.State == DeviceConnectionState.Connected;

    private void ShowDividers()
    {
        var isFirst = true;

        foreach (UIElement row in DevicesPanel.Children)
        {
            if (row.Visibility != Visibility.Visible)
            {
                continue;
            }

            if (row is HeadphoneRow headphones)
            {
                headphones.ShowDivider = !isFirst;
            }
            else
            {
                AirPodsDivider.Visibility = isFirst ? Visibility.Collapsed : Visibility.Visible;
            }

            isFirst = false;
        }
    }

    private void SizeToDevices()
    {
        if (NoDevicesPanel.Visibility == Visibility.Visible)
        {
            Height = NoDevicesHeight;
            return;
        }

        DevicesPanel.Measure(new Size(Width - WindowEdges, double.PositiveInfinity));

        var rows = DevicesPanel.Children.Cast<UIElement>().Where(row => row.Visibility == Visibility.Visible).ToList();
        var needsScrolling = rows.Count > MaximumRowsWithoutScrolling;
        var contentHeight = needsScrolling
            ? rows.Take(MaximumRowsWithoutScrolling).Sum(row => row.DesiredSize.Height) + DevicesPanel.Margin.Top + DevicesPanel.Margin.Bottom
            : DevicesPanel.DesiredSize.Height;

        // Scrolling is either always available or never, because a scrollbar that
        // comes and goes changes the width of the rows and shifts their buttons.
        DevicesScroller.VerticalScrollBarVisibility = needsScrolling ? ScrollBarVisibility.Visible : ScrollBarVisibility.Disabled;
        Height = Math.Ceiling(contentHeight + TitleBarHeight + WindowEdges);
    }

    private void ToggleSettings() => ShowSettings(SettingsPanel.Visibility != Visibility.Visible);

    private void ShowSettings(bool open)
    {
        SettingsPanel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        AirPodsParts.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
        AirPodsHeader.SettingsOpen = open;
    }

    private void EarDetectionToggle_Click(object sender, RoutedEventArgs e)
    {
        _settings.EarDetectionEnabled = EarDetectionToggle.IsChecked == true;
        _settings.Save();
        _earDetector.Reset();
        _pausedApps = [];
        ShowEarDetectionSetting();
    }

    private void ShowEarDetectionSetting()
    {
        EarDetectionToggle.IsChecked = _settings.EarDetectionEnabled;
        EarDetectionText.Text = _settings.EarDetectionEnabled ? "On" : "Off";
    }

    private void LowBatteryToggle_Click(object sender, RoutedEventArgs e)
    {
        _settings.LowBatteryEnabled = LowBatteryToggle.IsChecked == true;
        _settings.Save();
        _lowBatteryWatcher.Reset();
        ShowLowBatterySetting();
    }

    private void LowBatteryLevelSlider_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        LowBatteryLevelSlider.Value += Math.Sign(e.Delta) * LowBatteryLevelSlider.TickFrequency;
        e.Handled = true;
    }

    private void OnLowBatteryLevelChanged(int level)
    {
        _settings.LowBatteryLevel = level;
        _settings.Save();
        _lowBatteryWatcher.Reset();
        ShowLowBatterySetting();
    }

    private void ShowLowBatterySetting()
    {
        var enabled = _settings.LowBatteryEnabled;

        LowBatteryToggle.IsChecked = enabled;
        LowBatteryText.Text = enabled ? "On" : "Off";
        LowBatteryLevelText.Text = $"{(int)LowBatteryLevelSlider.Value}%";
        LowBatteryLevelSlider.IsEnabled = enabled;
        LowBatteryLevelRow.Opacity = enabled ? 1 : 0.5;
    }

    private void ApplyLowBatteryAlerts(AirPodsStatus status)
    {
        if (!_settings.LowBatteryEnabled || AirPodsHeader.State != DeviceConnectionState.Connected)
        {
            return;
        }

        var alerts = _lowBatteryWatcher.Update(status, _settings.LowBatteryLevel);
        if (alerts.Count > 0)
        {
            LowBatteryAlert?.Invoke(string.Join(Environment.NewLine, alerts));
        }
    }

    private void ApplyEarDetection(AirPodsStatus status)
    {
        _lastInEar = (status.LeftInEar, status.RightInEar);
        RunEarDetection();
    }

    // Also runs on a timer, because the next broadcast can be seconds away and a change shouldn't wait for it.
    private async void RunEarDetection()
    {
        if (_lastInEar is not { } inEar
            || !_settings.EarDetectionEnabled
            || AirPodsHeader.State != DeviceConnectionState.Connected)
        {
            return;
        }

        var action = _earDetector.Update(inEar.Left, inEar.Right, DateTimeOffset.Now);
        if (action == EarAction.None)
        {
            return;
        }

        // One at a time, because a pod can go back in while the pause is still running.
        await _earActionGate.WaitAsync();
        try
        {
            if (action == EarAction.Pause)
            {
                // Sound coming from the speakers shouldn't stop because a pod came out.
                if (await _airPodsConnection.IsDefaultOutputAsync())
                {
                    _pausedApps = _pausedApps.Union(await MediaSession.PauseAllPlayingAsync()).ToList();
                }
            }
            else
            {
                // Only what this app paused is resumed, so anything the user paused stays paused.
                var apps = _pausedApps;
                _pausedApps = [];
                await MediaSession.PlayAsync(apps);
            }
        }
        catch (Exception)
        {
            // A media app refusing the command shouldn't affect anything else.
        }
        finally
        {
            _earActionGate.Release();
        }
    }

    private void CaptionBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Hide();

    /// <summary>Shows the window above the bottom-right of the taskbar, or hides it if it's showing.</summary>
    public void Toggle()
    {
        if (IsVisible)
        {
            Hide();
        }
        else if (DateTimeOffset.Now - _lastHiddenByClickAway >= ClickAwayGrace)
        {
            ShowInCorner();
        }
    }

    public void ShowInCorner()
    {
        ArrangeRows();
        SizeToDevices();
        PositionInCorner();
        DevicesScroller.ScrollToTop();
        Show();
        Activate();
    }

    private void PositionInCorner()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - CornerGap;
        Top = workArea.Bottom - Height - CornerGap;
    }

    /// <summary>Lets the window really close, for when the app exits or Windows shuts down.</summary>
    public void AllowClose() => _closeAllowed = true;

    private void OnClickedAway()
    {
        if (!_closeAllowed)
        {
            _lastHiddenByClickAway = DateTimeOffset.Now;
            Hide();
        }
    }

    private void WatchOutput()
    {
        try
        {
            _outputWatcher = new DefaultOutputWatcher();
            _outputWatcher.Changed += () => Dispatcher.BeginInvoke(() =>
            {
                AirPodsHeader.RefreshOutputState();
                _otherRows.ForEach(row => row.RefreshOutputState());
            });
        }
        catch (Exception)
        {
            // The rows still recheck the output every few seconds without this.
        }
    }

    private void OnAirPodsStateChanged(DeviceConnectionState state)
    {
        if (state == DeviceConnectionState.Disconnected)
        {
            _tracker.Clear();
            _earDetector.Reset();
            _lastInEar = null;
            _pausedApps = [];
        }

        Refresh();
    }

    private void StartScanning()
    {
        _lastScanAttempt = DateTimeOffset.Now;
        SetScanError(null);

        try
        {
            _monitor.Start();
        }
        catch (Exception ex)
        {
            OnScanFailed($"Couldn't start Bluetooth scanning: {ex.Message}");
        }
    }

    private void OnStatusReceived(AirPodsStatus status)
    {
        SetScanError(null);
        if (CanShowBattery)
        {
            _tracker.Update(status, DateTimeOffset.Now);
            ApplyEarDetection(status);
            ApplyLowBatteryAlerts(status);
        }

        Refresh();
    }

    private void OnScanFailed(string message) => SetScanError(message);

    private void SetScanError(string? message)
    {
        _scanError = message;
        AirPodsHeader.Notice = message;
    }

    private void OnTick()
    {
        if (_scanError is not null && DateTimeOffset.Now - _lastScanAttempt >= RetryEvery)
        {
            StartScanning();
        }

        if (DateTimeOffset.Now - _lastDeviceCheck >= RetryEvery)
        {
            RefreshDevices();
        }

        Refresh();
    }

    private void Refresh()
    {
        var now = DateTimeOffset.Now;
        var snapshot = CanShowBattery ? _tracker.Current : null;

        AirPodsHeader.Title = snapshot?.Model ?? "AirPods";

        if (snapshot is not null)
        {
            RightBattery.Show(snapshot.Right, now);
            LeftBattery.Show(snapshot.Left, now);
            CaseBattery.Show(snapshot.Case, now);
        }
        else
        {
            RightBattery.Clear();
            LeftBattery.Clear();
            CaseBattery.Clear();
        }
    }
}

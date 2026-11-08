using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using BluetoothAudioManager.App.Helpers;
using BluetoothAudioManager.App.Models;
using BluetoothAudioManager.App.Services;
using BluetoothAudioManager.Core.Enums;
using BluetoothAudioManager.Core.Helpers;
using BluetoothAudioManager.Core.Interfaces;
using BluetoothAudioManager.Core.Models;
using BluetoothAudioManager.Core.Services;

namespace BluetoothAudioManager.App.Views;

public partial class MainWindow : Window
{
    private static readonly TimeSpan RetryEvery = TimeSpan.FromSeconds(5);

    // Clicking the tray icon first takes focus from the window, hiding it, so this stops that click reopening it.
    private static readonly TimeSpan ClickAwayGrace = TimeSpan.FromMilliseconds(500);
    private const double CornerGap = 12;
    private const double TitleBarHeight = 32;
    private const double WindowEdges = 2;
    private const double NoDevicesHeight = 240;
    private const int DefaultVisibleRows = 2;
    private const string AirPodsOrderKey = "AirPods";

    // How long a dropped row takes to settle, and other rows take to slide aside while one is dragged.
    private static readonly TimeSpan ReorderDuration = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan ShiftDuration = TimeSpan.FromMilliseconds(150);

    private readonly AirPodsMonitor _monitor = new();
    private readonly DeviceConnectionMonitor _airPodsConnection = new(name => PairedAudioDevices.IsAirPodsWithPods(name));
    private readonly List<HeadphoneRow> _otherRows = [];
    private readonly HashSet<HeadphoneRow> _usbAdapterRows = [];
    private readonly JabraHeadsets _jabra = new();
    private readonly BatteryTracker _tracker = new();
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly EarDetector _earDetector = new();
    private readonly LowBatteryWatcher _lowBatteryWatcher = new();
    private DefaultOutputWatcher? _outputWatcher;
    private AppUpdater? _updater;
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
    private double _smallestHeight;
    private double _allDevicesHeight;
    private DateTimeOffset _lastHiddenByClickAway;
    private bool _closeAllowed;
    private bool _updateBarDismissed;
    private string? _airPodsName;
    private string? _readyUpdateVersion;
    private AirPodsFamily? _airPodsFamily;
    private UIElement? _draggedRow;
    private List<UIElement> _dragRows = [];
    private List<double> _dragTops = [];
    private readonly Dictionary<UIElement, double> _dragShifts = [];
    private TranslateTransform _draggedSlide = new();
    private int _draggedIndex;
    private int _dropIndex;
    private double _dragStartY;

    private bool CanShowBattery =>
        AirPodsHeader.State is not (null or DeviceConnectionState.Disconnected);

    /// <summary>Raised with the title and message to show when a battery runs low.</summary>
    public event Action<string, string>? LowBatteryAlert;

    /// <summary>Raised when Update now is clicked in the update bar.</summary>
    public event Action? UpdateClicked;

    /// <summary>Whether ear detection paused media that it will resume when the pods go back in.</summary>
    public bool HasPausedMedia => _pausedApps.Count > 0;

    // The update bar's height with its margin, when it's showing.
    private double UpdateBarHeight
    {
        get
        {
            if (UpdateBar.Visibility != Visibility.Visible)
            {
                return 0;
            }

            UpdateBar.Measure(new Size(Width - WindowEdges, double.PositiveInfinity));
            return UpdateBar.DesiredSize.Height;
        }
    }

    public MainWindow()
    {
        // Before the window is built, so it never appears in the other theme.
        WindowBackdrop.IsLight = _settings.UseLightTheme;
        InitializeComponent();
        WindowBackdrop.Transparency = _settings.Transparency;
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
        AppSettingsView.ThemeChanged += () => _otherRows.ForEach(row => row.ShowCurrentPicture());
        AppSettingsView.UpdateClicked += () => UpdateClicked?.Invoke();
        AppSettingsView.UpdateFoundByCheck += version => ShowUpdateReady(version, explicitlyChecked: true);

        // While a device is dragged, the area holding the devices has the mouse, so these follow it to the drop.
        AirPodsHeader.DragStarted += () => StartDrag(AirPodsSection);
        DevicesArea.MouseMove += (_, e) => MoveDrag(e.GetPosition(DevicesPanel));
        DevicesArea.MouseLeftButtonUp += (_, _) => EndDrag(drop: true);
        DevicesArea.LostMouseCapture += (_, _) => EndDrag(drop: false);

        // Closed on hiding, so the window always reopens showing the batteries.
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is false)
            {
                ShowSettings(false);
                _otherRows.ForEach(row => row.ShowSettings(false));
                ShowAppSettings(false);
            }
        };
        ShowEarDetectionSetting();
        AirPodsLowBattery.Show(_settings.LowBatteryEnabled, _settings.LowBatteryLevel);
        AirPodsLowBattery.Changed += OnAirPodsLowBatteryChanged;

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
            _jabra.Dispose();
        };
    }

    /// <summary>Starts following the devices, whether or not the window is showing.</summary>
    public void StartMonitoring()
    {
        StartScanning();
        AirPodsHeader.Follow(_airPodsConnection);
        RefreshDevices();
        WatchOutput();
        StartJabra();
        _timer.Start();
        _earDetectionTimer.Tick += (_, _) => RunEarDetection();
        _earDetectionTimer.Start();
    }

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

    /// <summary>Lets the window really close, for when the app exits or Windows shuts down.</summary>
    public void AllowClose() => _closeAllowed = true;

    /// <summary>Gives the settings the updater to ask whether it checks for updates by itself.</summary>
    internal void UseUpdater(AppUpdater updater)
    {
        _updater = updater;
        AppSettingsView.Updater = updater;
        ReleaseNotesButton.IsEnabled = updater.IsInstalled;
    }

    /// <summary>Shows the bar offering to install a downloaded update.</summary>
    public void ShowUpdateReady(string version, bool explicitlyChecked = false)
    {
        _readyUpdateVersion = version;
        AppSettingsView.ShowUpdateReady();
        UpdateText.Text = $"Version {version} is ready";
        if (explicitlyChecked)
        {
            _updateBarDismissed = false;
        }

        UpdateBar.Visibility = _updateBarDismissed ? Visibility.Collapsed : Visibility.Visible;

        if (IsVisible)
        {
            SizeToDevices();
        }
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
            ShowDevices(await PairedAudioDevices.FindAsync());
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

    private void ShowDevices(IReadOnlyList<PairedAudioDevice> devices)
    {
        // Every pair of AirPods with pods shares the one AirPods row; anything else gets a row of its own.
        var airPods = devices.Where(device => PairedAudioDevices.IsAirPodsWithPods(device)).ToList();
        var others = devices.Except(airPods).ToList();

        foreach (var row in _otherRows.Where(row => others.All(device => device.Name != row.DeviceName)).ToList())
        {
            if (row == _draggedRow)
            {
                EndDrag(drop: false);
            }

            DevicesPanel.Children.Remove(row);
            _otherRows.Remove(row);
            _usbAdapterRows.Remove(row);
            row.Dispose();
        }

        var added = others.Where(device => _otherRows.All(row => row.DeviceName != device.Name)).ToList();
        foreach (var device in added)
        {
            var name = device.Name;
            IAudioDeviceConnection connection = device.IsUsbAdapter
                ? new UsbHeadsetMonitor(name, _jabra)
                : new DeviceConnectionMonitor(name);
            // Windows stores only a level, so only Jabra's kit can say a headset is charging.
            Func<Task<BatteryReading?>> readBattery = device.IsUsbAdapter
                ? () => Task.FromResult(_jabra.FindByAdapter(name) is { } headset
                    ? new BatteryReading(headset.BatteryPercent, headset.IsCharging)
                    : null)
                : async () => new BatteryReading(await DeviceBatteryReader.ReadPercentAsync(name), false);
            var row = new HeadphoneRow(name, TitleFor(device), () => PictureFor(device), ProductPictures.EarbudsForDeviceName(device.Name), _settings, connection, readBattery);
            if (device.IsUsbAdapter)
            {
                _usbAdapterRows.Add(row);
            }

            row.LowBatteryAlert += (title, message) => LowBatteryAlert?.Invoke(title, message);
            row.DragStarted += () => StartDrag(row);
            _otherRows.Add(row);
            DevicesPanel.Children.Add(row);
        }

        if (airPods.FirstOrDefault() is { } shown)
        {
            _airPodsName = shown.Name;
            ShowAirPodsPictures(AppleProducts.FamilyOf(shown.AppleProductId));
            Refresh();
        }

        AirPodsSection.Visibility = airPods.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        NoDevicesPanel.Visibility = devices.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // A device paired again goes back to its saved place, and a new one goes last.
        if (added.Count > 0 && _draggedRow is null)
        {
            ArrangeRows();
        }

        ShowDividers();

        if (!_devicesShownOnce)
        {
            _devicesShownOnce = true;
            SizeToDevices();
            PositionInCorner();
        }
    }

    // A headset on a USB adapter is named after the headset once Jabra's kit reports it, and after the adapter until then.
    private string TitleFor(PairedAudioDevice device) =>
        device.IsUsbAdapter ? _jabra.HeadsetNameFor(device.Name) ?? device.Name : device.Name;

    // The picture matching the device's name, or the generic headphones placeholder when there's none.
    // AirPods Max follow the theme, and renamed ones are still recognized by their product ID.
    private ImageSource PictureFor(PairedAudioDevice device)
    {
        if (AppleProducts.FamilyOf(device.AppleProductId) == AirPodsFamily.AirPodsMax || PairedAudioDevices.IsAirPodsMax(device.Name))
        {
            return ProductPictures.AirPodsMaxFor(device.AppleProductId, WindowBackdrop.IsLight);
        }

        return ProductPictures.ForDeviceName(device.Name) ?? (ImageSource)FindResource("GenericHeadphonesImage");
    }

    private void ShowAirPodsPictures(AirPodsFamily family)
    {
        if (family == _airPodsFamily)
        {
            return;
        }

        _airPodsFamily = family;
        (RightPodPicture.Source, LeftPodPicture.Source, CasePicture.Source) = ProductPictures.ForAirPods(family);
    }

    // Puts the rows in the order the user dragged them into. Connecting or disconnecting never moves a row.
    private void ArrangeRows()
    {
        var rows = DeviceOrder.Sort(DevicesPanel.Children.Cast<UIElement>(), OrderKeyOf, _settings.DeviceOrder);

        DevicesPanel.Children.Clear();
        rows.ForEach(row => DevicesPanel.Children.Add(row));
        ShowDividers();
    }

    // Each moved row starts drawn where it was, then slides to its new place.
    // The places before include any slide still running, so a quick second drop starts from where the rows are seen.
    private void SlideIntoPlace(Dictionary<UIElement, double> before)
    {
        foreach (var row in before.Keys)
        {
            row.RenderTransform = Transform.Identity;
        }

        DevicesPanel.UpdateLayout();

        foreach (var (row, oldTop) in before)
        {
            var offset = oldTop - TopOf(row);
            if (Math.Abs(offset) < 0.5)
            {
                continue;
            }

            var slide = new TranslateTransform(0, offset);
            row.RenderTransform = slide;
            slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(offset, 0, ReorderDuration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
            });
        }
    }

    private double TopOf(UIElement row) => row.TranslatePoint(new Point(0, 0), DevicesPanel).Y;

    private static string OrderKeyOf(UIElement row) =>
        row is HeadphoneRow headphones ? headphones.DeviceName : AirPodsOrderKey;

    private List<UIElement> VisibleRows() =>
        DevicesPanel.Children.Cast<UIElement>().Where(row => row.Visibility == Visibility.Visible).ToList();

    // Lifts the row, and notes where every row sits in the list, as the drag works from those places.
    private void StartDrag(UIElement row)
    {
        foreach (var shown in VisibleRows())
        {
            shown.RenderTransform = Transform.Identity;
        }

        DevicesPanel.UpdateLayout();
        _dragRows = VisibleRows();
        _dragTops = _dragRows.Select(TopOf).ToList();
        _dragShifts.Clear();
        _draggedRow = row;
        _draggedIndex = _dropIndex = _dragRows.IndexOf(row);
        _dragStartY = Mouse.GetPosition(DevicesPanel).Y;
        _draggedSlide = new TranslateTransform();
        row.RenderTransform = _draggedSlide;
        Lift(row, true);
        DevicesArea.CaptureMouse();
    }

    // The dragged row follows the mouse, and the rows it passes slide aside to open a gap where it would land.
    private void MoveDrag(Point mouse)
    {
        if (_draggedRow is not { } row)
        {
            return;
        }

        var top = _dragTops[_draggedIndex];
        var height = row.RenderSize.Height;
        var lastBottom = _dragTops[^1] + _dragRows[^1].RenderSize.Height;
        var offset = Math.Clamp(mouse.Y - _dragStartY, -top, lastBottom - height - top);
        _draggedSlide.Y = offset;

        // A row is passed once the dragged row's leading edge crosses its middle: the top edge going up, the bottom edge going down.
        // Edges rather than the dragged row's middle, because the row can't leave the list, and a tall row's middle
        // would never get past a shorter first or last row.
        var draggedTop = top + offset;
        var draggedBottom = draggedTop + height;
        _dropIndex = _draggedIndex;

        for (var index = 0; index < _dragRows.Count; index++)
        {
            if (index == _draggedIndex)
            {
                continue;
            }

            var middle = _dragTops[index] + _dragRows[index].RenderSize.Height / 2;
            var passed = index < _draggedIndex ? draggedTop < middle : draggedBottom > middle;
            if (passed)
            {
                _dropIndex += index < _draggedIndex ? -1 : 1;
            }

            ShiftRow(_dragRows[index], !passed ? 0 : index < _draggedIndex ? height : -height);
        }
    }

    // Slides a row aside, starting a new slide only when where it should be changes.
    private void ShiftRow(UIElement row, double shift)
    {
        if (_dragShifts.TryGetValue(row, out var current) && current == shift)
        {
            return;
        }

        _dragShifts[row] = shift;
        if (row.RenderTransform is not TranslateTransform slide || slide.IsFrozen)
        {
            slide = new TranslateTransform();
            row.RenderTransform = slide;
        }

        slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(shift, ShiftDuration)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        });
    }

    // On a drop the row moves to its new place in the list; either way, every row slides from where it's seen to where it belongs.
    private void EndDrag(bool drop)
    {
        if (_draggedRow is not { } row)
        {
            return;
        }

        // Cleared first, because releasing the mouse comes back here through LostMouseCapture.
        _draggedRow = null;
        DevicesArea.ReleaseMouseCapture();

        var before = VisibleRows().ToDictionary(shown => shown, TopOf);
        Lift(row, false);

        var moved = drop && _dropIndex != _draggedIndex;
        if (moved)
        {
            var others = _dragRows.Where(other => other != row).ToList();
            var target = _dropIndex < others.Count ? others[_dropIndex] : null;
            DevicesPanel.Children.Remove(row);
            DevicesPanel.Children.Insert(target is null ? DevicesPanel.Children.Count : DevicesPanel.Children.IndexOf(target), row);
            ShowDividers();
        }

        SlideIntoPlace(before);

        if (moved)
        {
            _settings.DeviceOrder = DeviceOrder.Merge(_settings.DeviceOrder, VisibleRows().Select(OrderKeyOf).ToList());
            _settings.Save();
        }
    }

    private void Lift(UIElement row, bool lifted)
    {
        if (row is HeadphoneRow headphones)
        {
            headphones.Lifted = lifted;
        }
        else
        {
            LiftedCard.Apply(AirPodsSection, lifted);
            AirPodsDivider.Opacity = lifted ? 0 : 1;
        }
    }

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
                AirPodsDivider.Visibility = isFirst ? Visibility.Hidden : Visibility.Visible;
            }

            isFirst = false;
        }
    }

    private void SizeToDevices()
    {
        double defaultHeight;

        if (NoDevicesPanel.Visibility == Visibility.Visible)
        {
            _smallestHeight = _allDevicesHeight = defaultHeight = NoDevicesHeight + UpdateBarHeight;
        }
        else
        {
            DevicesPanel.Measure(new Size(Width - WindowEdges, double.PositiveInfinity));

            var rows = DevicesPanel.Children.Cast<UIElement>().Where(row => row.Visibility == Visibility.Visible).ToList();

            double HeightOfRows(int count) => Math.Ceiling(
                rows.Take(count).Sum(row => row.DesiredSize.Height)
                + DevicesPanel.Margin.Top + DevicesPanel.Margin.Bottom + TitleBarHeight + UpdateBarHeight + WindowEdges);

            _smallestHeight = HeightOfRows(1);
            _allDevicesHeight = HeightOfRows(rows.Count);
            defaultHeight = HeightOfRows(DefaultVisibleRows);
        }

        ResizeTo(_settings.WindowHeight ?? defaultHeight);
    }

    private void ResizeTo(double height)
    {
        var screenLimit = SystemParameters.WorkArea.Height - 2 * CornerGap;
        var largest = Math.Max(_smallestHeight, Math.Min(_allDevicesHeight, screenLimit));

        Height = Math.Clamp(height, _smallestHeight, largest);

        // Only shown when something doesn't fit, which the user can choose by making the window smaller.
        DevicesScroller.VerticalScrollBarVisibility = Height < _allDevicesHeight
            ? ScrollBarVisibility.Visible
            : ScrollBarVisibility.Disabled;
    }

    private void TopEdge_DragDelta(object sender, DragDeltaEventArgs e)
    {
        // Keeps the bottom edge where it is, as the window sits on the taskbar.
        var bottom = Top + Height;
        ResizeTo(Height - e.VerticalChange);
        Top = bottom - Height;
    }

    private void BottomEdge_DragDelta(object sender, DragDeltaEventArgs e) => ResizeTo(Height + e.VerticalChange);

    private void ResizeEdge_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        _settings.WindowHeight = Height;
        _settings.Save();
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

    private void OnAirPodsLowBatteryChanged(bool enabled, int level)
    {
        _settings.LowBatteryEnabled = enabled;
        _settings.LowBatteryLevel = level;
        _settings.Save();
        _lowBatteryWatcher.Reset();
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
            LowBatteryAlert?.Invoke("AirPods battery low", string.Join(Environment.NewLine, alerts));
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

    private void UpdateButton_Click(object sender, RoutedEventArgs e) => UpdateClicked?.Invoke();

    private void DismissUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        _updateBarDismissed = true;
        UpdateBar.Visibility = Visibility.Collapsed;
        if (IsVisible)
        {
            SizeToDevices();
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e) => ShowAppSettings(AppSettingsArea.Visibility != Visibility.Visible);

    private async void ReleaseNotesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_updater is null || !_updater.IsInstalled)
        {
            return;
        }

        ShowAppSettings(false);
        DevicesView.Visibility = Visibility.Collapsed;
        ReleaseNotesArea.Visibility = Visibility.Visible;
        ReleaseNotesVersion.Text = "Loading...";
        ReleaseNotesText.Text = "";
        ReleaseNotesButton.IsEnabled = false;

        try
        {
            if (await _updater.LatestReleaseNotesAsync() is { } release)
            {
                ReleaseNotesVersion.Text = $"Version {release.Version}";
                ReleaseNotesText.Text = string.IsNullOrWhiteSpace(release.Notes)
                    ? "No release notes are available for this version."
                    : release.Notes;
            }
            else
            {
                ReleaseNotesVersion.Text = "";
                ReleaseNotesText.Text = "Couldn't load release notes. Try again later.";
            }
        }
        finally
        {
            ReleaseNotesButton.IsEnabled = true;
        }

        ReleaseNotesArea.ScrollToTop();
    }

    private void ReleaseNotesBackButton_Click(object sender, RoutedEventArgs e)
    {
        ReleaseNotesArea.Visibility = Visibility.Collapsed;
        DevicesView.Visibility = Visibility.Visible;
        DevicesScroller.ScrollToTop();
    }

    // The app's settings take the devices' place, and the gear is filled with the accent color while they're open.
    private void ShowAppSettings(bool open)
    {
        ReleaseNotesArea.Visibility = Visibility.Collapsed;
        if (open)
        {
            AppSettingsView.Show(_settings);
            AppSettingsArea.ScrollToTop();
            SettingsButton.SetResourceReference(BackgroundProperty, "AccentFillColorDefaultBrush");
            SettingsButton.SetResourceReference(ForegroundProperty, "TextOnAccentFillColorPrimaryBrush");
        }
        else
        {
            SettingsButton.ClearValue(BackgroundProperty);
            SettingsButton.ClearValue(ForegroundProperty);
        }

        AppSettingsArea.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        DevicesView.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
    }

    private void PositionInCorner()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - CornerGap;
        Top = workArea.Bottom - Height - CornerGap;
    }

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

    private async void StartJabra()
    {
        _jabra.Changed += () => Dispatcher.BeginInvoke(() => OnJabraChanged());

        try
        {
            // Without a partner key the kit may refuse to start, and Jabra rows then show no battery.
            await _jabra.StartAsync(_settings.JabraPartnerKey ?? "");
        }
        catch (Exception)
        {
            // The Jabra row still works without the battery level.
        }
    }

    private void OnJabraChanged()
    {
        foreach (var row in _usbAdapterRows)
        {
            row.Title = _jabra.HeadsetNameFor(row.DeviceName) ?? row.DeviceName;
            row.RefreshConnection();
            row.RefreshBattery();
        }
    }

    private void OnAirPodsStateChanged(DeviceConnectionState state)
    {
        // Settings can only be opened while connected, so they close when the AirPods disconnect.
        if (state != DeviceConnectionState.Connected)
        {
            ShowSettings(false);
        }

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

        AirPodsHeader.Title = _airPodsName ?? PairedAudioDevices.AirPodsName;

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

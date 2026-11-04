using System.Windows;
using System.Windows.Threading;
using BluetoothAudioManager.Core;

namespace BluetoothAudioManager.App;

public partial class MainWindow : Window
{
    private static readonly TimeSpan RetryEvery = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(15);

    private readonly AirPodsMonitor _monitor = new();
    private readonly AirPodsConnectionMonitor _connection = new();
    private readonly BatteryTracker _tracker = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private string? _scanError;
    private DateTimeOffset _lastScanAttempt;
    private DateTimeOffset _lastConnectionCheck;
    private bool _findingDevices;
    private AirPodsConnectionState? _connectionState;
    private string? _busyMessage;
    private string? _failureMessage;

    private bool CanShowBattery =>
        _connectionState is not (null or AirPodsConnectionState.Disconnected);

    public MainWindow()
    {
        InitializeComponent();
        TitleBarTheme.Follow(this);

        _monitor.StatusReceived += status => Dispatcher.BeginInvoke(() => OnStatusReceived(status));
        _monitor.ScanFailed += error => Dispatcher.BeginInvoke(() => OnScanFailed($"Bluetooth scanning stopped: {error}."));
        _connection.StateChanged += state => Dispatcher.BeginInvoke(() => ShowConnection(state));
        _timer.Tick += (_, _) => OnTick();

        Loaded += (_, _) =>
        {
            StartScanning();
            FindConnectionDevices();
            _timer.Start();
        };
        Closed += (_, _) =>
        {
            _timer.Stop();
            _monitor.Dispose();
            _connection.Dispose();
        };
    }

    private async void FindConnectionDevices()
    {
        _lastConnectionCheck = DateTimeOffset.Now;
        _findingDevices = true;

        try
        {
            await _connection.FindDevicesAsync();
        }
        catch (Exception ex)
        {
            _connectionState = AirPodsConnectionState.NotPaired;
            ConnectionText.Text = $"Couldn't check the connection: {ex.Message}";
        }
        finally
        {
            _findingDevices = false;
        }
    }

    private void ShowConnection(AirPodsConnectionState state)
    {
        if (state != _connectionState && _busyMessage is null)
        {
            _failureMessage = null;
        }

        _connectionState = state;
        if (state == AirPodsConnectionState.Disconnected)
        {
            _tracker.Clear();
        }

        UpdateConnectionDisplay();
        Refresh();
    }

    private void UpdateConnectionDisplay()
    {
        var connected = _connectionState == AirPodsConnectionState.Connected;
        var paired = connected || _connectionState == AirPodsConnectionState.Disconnected;

        ConnectButton.Content = connected ? "Disconnect" : "Connect";
        ConnectButton.IsEnabled = _busyMessage is null && paired;
        ReconnectButton.IsEnabled = _busyMessage is null && connected;
        ConnectionText.Text = _busyMessage ?? _failureMessage ?? _connectionState switch
        {
            AirPodsConnectionState.Connected => "Connected to this PC",
            AirPodsConnectionState.Disconnected => "Disconnected",
            _ => "No paired AirPods found in Windows",
        };
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        if (_connectionState == AirPodsConnectionState.Connected)
        {
            await RunConnectionActionAsync("Disconnecting", "Disconnect failed", _connection.DisconnectAsync);
        }
        else
        {
            await RunConnectionActionAsync("Connecting", "Connection failed", _connection.ConnectAsync);
        }
    }

    private async void ReconnectButton_Click(object sender, RoutedEventArgs e)
    {
        await RunConnectionActionAsync("Reconnecting", "Reconnect failed", _connection.ReconnectAsync);
    }

    private async Task RunConnectionActionAsync(string busyMessage, string failureMessage, Func<TimeSpan, Task<bool>> action)
    {
        _busyMessage = busyMessage;
        _failureMessage = null;
        UpdateConnectionDisplay();

        bool succeeded;
        try
        {
            succeeded = await action(ConnectTimeout);
        }
        catch (Exception)
        {
            succeeded = false;
        }

        _busyMessage = null;
        _failureMessage = succeeded ? null : failureMessage;
        UpdateConnectionDisplay();
    }

    private void StartScanning()
    {
        _lastScanAttempt = DateTimeOffset.Now;
        _scanError = null;

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
        _scanError = null;
        if (CanShowBattery)
        {
            _tracker.Update(status, DateTimeOffset.Now);
        }

        Refresh();
    }

    private void OnScanFailed(string message)
    {
        _scanError = message;
        Refresh();
    }

    private void OnTick()
    {
        if (_scanError is not null && DateTimeOffset.Now - _lastScanAttempt >= RetryEvery)
        {
            StartScanning();
        }

        if (!_findingDevices
            && _connection.State == AirPodsConnectionState.NotPaired
            && DateTimeOffset.Now - _lastConnectionCheck >= RetryEvery)
        {
            FindConnectionDevices();
        }

        Refresh();
    }

    private void Refresh()
    {
        var now = DateTimeOffset.Now;

        if (_tracker.Current is { } snapshot)
        {
            ModelText.Text = snapshot.Model;
            LeftBattery.Show(snapshot.Left, now);
            RightBattery.Show(snapshot.Right, now);
            CaseBattery.Show(snapshot.Case, now);
        }
        else
        {
            ModelText.Text = _connectionState == AirPodsConnectionState.Disconnected ? "AirPods" : "Looking for AirPods";
            LeftBattery.Clear();
            RightBattery.Clear();
            CaseBattery.Clear();
        }

        StatusText.Text = DescribeStatus(_tracker.Current, now);
    }

    private string DescribeStatus(AirPodsSnapshot? snapshot, DateTimeOffset now)
    {
        if (_scanError is not null)
        {
            return $"{_scanError} Retrying.";
        }

        if (_connectionState == AirPodsConnectionState.Disconnected)
        {
            return "";
        }

        if (snapshot is null)
        {
            return "Open the case near this PC, or take a pod out.";
        }

        var age = now - snapshot.UpdatedAt;
        return age > TrackedBattery.FreshFor
            ? $"Last heard {TrackedBattery.DescribeAge(age)}. Open the case to refresh."
            : "Live";
    }
}

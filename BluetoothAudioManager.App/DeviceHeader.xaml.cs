using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using BluetoothAudioManager.Core;

namespace BluetoothAudioManager.App;

/// <summary>A device's title, connection buttons and messages.</summary>
public partial class DeviceHeader : UserControl
{
    private static readonly TimeSpan ActionTimeout = TimeSpan.FromSeconds(15);

    private readonly DispatcherTimer _findTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private DeviceConnectionMonitor? _connection;
    private bool _findingDevices;
    private string? _busyMessage;
    private string? _failureMessage;
    private string? _notice;
    private bool _isDefaultOutput;

    public DeviceHeader()
    {
        InitializeComponent();
        _findTimer.Tick += (_, _) => OnFindTimerTick();
    }

    public event Action<DeviceConnectionState>? StateChanged;

    public event Action? SettingsClicked;

    public bool SettingsOpen
    {
        set => SetHighlight(SettingsButton, value);
    }

    public DeviceConnectionState? State { get; private set; }

    public string Title
    {
        set => TitleText.Text = value;
    }

    public bool ShowSettings
    {
        set => SettingsButton.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>A message from the owner, shown when nothing more urgent is.</summary>
    public string? Notice
    {
        set
        {
            _notice = value;
            UpdateDisplay();
        }
    }

    public void Follow(DeviceConnectionMonitor connection)
    {
        _connection = connection;
        connection.StateChanged += state => Dispatcher.BeginInvoke(() => OnStateChanged(state));
        FindDevices();
        _findTimer.Start();
    }

    public void Stop() => _findTimer.Stop();

    private void OnFindTimerTick()
    {
        if (!_findingDevices && _busyMessage is null && _connection is not null)
        {
            FindDevices();
        }

        RefreshOutputState();
    }

    private async void FindDevices()
    {
        _findingDevices = true;

        try
        {
            await _connection!.FindDevicesAsync();
        }
        catch (Exception)
        {
            OnStateChanged(DeviceConnectionState.NotPaired);
        }
        finally
        {
            _findingDevices = false;
        }
    }

    private void OnStateChanged(DeviceConnectionState state)
    {
        if (state != State && _busyMessage is null)
        {
            _failureMessage = null;
        }

        State = state;
        UpdateDisplay();
        RefreshOutputState();
        StateChanged?.Invoke(state);
    }

    public async void RefreshOutputState()
    {
        if (_connection is null)
        {
            return;
        }

        try
        {
            _isDefaultOutput = State == DeviceConnectionState.Connected && await _connection.IsDefaultOutputAsync();
        }
        catch (Exception)
        {
            _isDefaultOutput = false;
        }

        UpdateDisplay();
    }

    private async void OutputButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isDefaultOutput)
        {
            return;
        }

        // Highlighted straight away, because the switch itself takes a moment.
        _isDefaultOutput = true;
        UpdateDisplay();

        bool switched;
        try
        {
            switched = await _connection!.SetAsDefaultOutputAsync();
        }
        catch (Exception)
        {
            switched = false;
        }

        _failureMessage = switched ? null : "Couldn't switch the system output";
        UpdateDisplay();
        RefreshOutputState();
    }

    private void UpdateDisplay()
    {
        var connected = State == DeviceConnectionState.Connected;
        var paired = connected || State == DeviceConnectionState.Disconnected;

        OutputButton.IsEnabled = _busyMessage is null && connected;
        ShowOutputHighlight(_isDefaultOutput && connected);
        ConnectButton.Content = connected ? "Disconnect" : "Connect";
        ConnectButton.IsEnabled = _busyMessage is null && paired;
        ReconnectButton.IsEnabled = _busyMessage is null && connected;
        MessageText.Text = _busyMessage
            ?? _failureMessage
            ?? _notice
            ?? (State == DeviceConnectionState.NotPaired ? "Not found in Windows' paired devices" : "");
    }

    private void ShowOutputHighlight(bool isOutput)
    {
        var description = isOutput ? "Current system output" : "Use as system output";
        OutputButton.ToolTip = description;
        AutomationProperties.SetName(OutputButton, description);
        SetHighlight(OutputButton, isOutput);
    }

    private static void SetHighlight(Button button, bool highlighted)
    {
        if (highlighted)
        {
            button.SetResourceReference(BackgroundProperty, "AccentFillColorDefaultBrush");
            button.SetResourceReference(ForegroundProperty, "TextOnAccentFillColorPrimaryBrush");
        }
        else
        {
            button.ClearValue(BackgroundProperty);
            button.ClearValue(ForegroundProperty);
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e) => SettingsClicked?.Invoke();

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        if (State == DeviceConnectionState.Connected)
        {
            await RunActionAsync("Disconnecting", "Disconnect failed", _connection!.DisconnectAsync);
        }
        else
        {
            await RunActionAsync("Connecting", "Connection failed", _connection!.ConnectAsync);
        }
    }

    private async void ReconnectButton_Click(object sender, RoutedEventArgs e)
    {
        await RunActionAsync("Reconnecting", "Reconnect failed", _connection!.ReconnectAsync);
    }

    private async Task RunActionAsync(string busyMessage, string failureMessage, Func<TimeSpan, Task<DeviceActionResult>> action)
    {
        _busyMessage = busyMessage;
        _failureMessage = null;
        UpdateDisplay();

        DeviceActionResult result;
        try
        {
            result = await action(ActionTimeout);
        }
        catch (Exception)
        {
            result = DeviceActionResult.TimedOut;
        }

        _busyMessage = null;
        _failureMessage = result switch
        {
            DeviceActionResult.Succeeded => null,
            DeviceActionResult.NoAudioOutput => $"{failureMessage}: pair the device in Windows again",
            _ => failureMessage,
        };
        UpdateDisplay();
    }
}

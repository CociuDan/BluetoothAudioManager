using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using BluetoothAudioManager.Core.Enums;

namespace BluetoothAudioManager.Core.Services;

/// <summary>Follows and changes Windows' connection status for a paired audio device. Events are raised on a background thread.</summary>
/// <param name="deviceName">Text the device's Windows name must contain, so a renamed device without it isn't found.</param>
public sealed class DeviceConnectionMonitor(string deviceName) : IDisposable
{
    private static readonly TimeSpan ReconnectPause = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ConnectRetryEvery = TimeSpan.FromSeconds(3);

    private IReadOnlyList<BluetoothDevice> _devices = [];

    public event Action<DeviceConnectionState>? StateChanged;

    public DeviceConnectionState State =>
        _devices.Count == 0 ? DeviceConnectionState.NotPaired
        : _devices.Any(device => device.ConnectionStatus == BluetoothConnectionStatus.Connected) ? DeviceConnectionState.Connected
        : DeviceConnectionState.Disconnected;

    public bool Matches(string name) =>
        name.Contains(deviceName, StringComparison.OrdinalIgnoreCase);

    /// <summary>Refreshes the device list, so a device paired or removed while the app runs is noticed.</summary>
    public async Task FindDevicesAsync()
    {
        var paired = await DeviceInformation.FindAllAsync(BluetoothDevice.GetDeviceSelectorFromPairingState(true));
        var devices = new List<BluetoothDevice>();

        foreach (var info in paired.Where(info => Matches(info.Name)))
        {
            var device = await BluetoothDevice.FromIdAsync(info.Id);
            if (device is not null)
            {
                device.ConnectionStatusChanged += OnConnectionStatusChanged;
                devices.Add(device);
            }
        }

        var previous = _devices;
        _devices = devices;
        Release(previous);
        StateChanged?.Invoke(State);
    }

    /// <summary>Asks Windows to connect the device, then waits for it to actually connect.</summary>
    public async Task<DeviceActionResult> ConnectAsync(TimeSpan timeout)
    {
        var deadline = DateTimeOffset.Now + timeout;
        var anyRequested = false;

        // A request only means the driver tried, and one sent while the device is still
        // disconnecting can be lost, so keep asking until it connects or time runs out.
        while (true)
        {
            // Every output is asked, because each one carries its own audio connection to the device.
            var requests = await Task.Run(() =>
                BluetoothAudioEndpoint.Find(Matches).Count(endpoint => endpoint.RequestConnect()));
            anyRequested |= requests > 0;

            var remaining = deadline - DateTimeOffset.Now;
            var wait = remaining < ConnectRetryEvery ? remaining : ConnectRetryEvery;
            if (await WaitForStateAsync(DeviceConnectionState.Connected, wait))
            {
                return DeviceActionResult.Succeeded;
            }

            if (DateTimeOffset.Now >= deadline)
            {
                return anyRequested ? DeviceActionResult.TimedOut : DeviceActionResult.NoAudioOutput;
            }
        }
    }

    /// <summary>Asks Windows to disconnect the device, then waits for it to actually disconnect.</summary>
    public async Task<DeviceActionResult> DisconnectAsync(TimeSpan timeout)
    {
        var requests = await Task.Run(() =>
            BluetoothAudioEndpoint.Find(Matches).Count(endpoint => endpoint.RequestDisconnect()));

        return await FinishAsync(requests, DeviceConnectionState.Disconnected, timeout);
    }

    /// <summary>Disconnects the device, then connects it again.</summary>
    public async Task<DeviceActionResult> ReconnectAsync(TimeSpan timeout)
    {
        var disconnected = await DisconnectAsync(timeout);
        if (disconnected != DeviceActionResult.Succeeded)
        {
            return disconnected;
        }

        await Task.Delay(ReconnectPause);
        return await ConnectAsync(timeout);
    }

    public Task<bool> IsDefaultOutputAsync() =>
        Task.Run(() => BluetoothAudioEndpoint.IsDefaultOutput(Matches));

    /// <summary>Makes this device the system output.</summary>
    /// <returns>Whether Windows accepted the change.</returns>
    public Task<bool> SetAsDefaultOutputAsync() =>
        Task.Run(() => BluetoothAudioEndpoint.SetDefaultOutput(Matches));

    private async Task<DeviceActionResult> FinishAsync(int requests, DeviceConnectionState state, TimeSpan timeout)
    {
        if (requests == 0)
        {
            return DeviceActionResult.NoAudioOutput;
        }

        return await WaitForStateAsync(state, timeout) ? DeviceActionResult.Succeeded : DeviceActionResult.TimedOut;
    }

    private async Task<bool> WaitForStateAsync(DeviceConnectionState state, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.Now + timeout;
        while (DateTimeOffset.Now < deadline)
        {
            if (State == state)
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        return State == state;
    }

    private void Release(IReadOnlyList<BluetoothDevice> devices)
    {
        foreach (var device in devices)
        {
            device.ConnectionStatusChanged -= OnConnectionStatusChanged;
            device.Dispose();
        }
    }

    private void OnConnectionStatusChanged(BluetoothDevice sender, object args) => StateChanged?.Invoke(State);

    public void Dispose()
    {
        Release(_devices);
        _devices = [];
    }
}

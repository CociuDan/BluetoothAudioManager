using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace BluetoothAudioManager.Core;

public enum AirPodsConnectionState
{
    NotPaired,
    Disconnected,
    Connected,
}

/// <summary>Follows Windows' connection status for paired AirPods. Events are raised on a background thread.</summary>
public sealed class AirPodsConnectionMonitor : IDisposable
{
    private static readonly TimeSpan ReconnectPause = TimeSpan.FromSeconds(1);

    private IReadOnlyList<BluetoothDevice> _devices = [];

    public event Action<AirPodsConnectionState>? StateChanged;

    public AirPodsConnectionState State =>
        _devices.Count == 0 ? AirPodsConnectionState.NotPaired
        : _devices.Any(device => device.ConnectionStatus == BluetoothConnectionStatus.Connected) ? AirPodsConnectionState.Connected
        : AirPodsConnectionState.Disconnected;

    /// <summary>Paired devices are matched by name, so AirPods renamed without "AirPods" aren't found.</summary>
    public static bool IsAirPods(string deviceName) =>
        deviceName.Contains("AirPods", StringComparison.OrdinalIgnoreCase);

    public async Task FindDevicesAsync()
    {
        ReleaseDevices();

        var paired = await DeviceInformation.FindAllAsync(BluetoothDevice.GetDeviceSelectorFromPairingState(true));
        var devices = new List<BluetoothDevice>();

        foreach (var info in paired.Where(info => IsAirPods(info.Name)))
        {
            var device = await BluetoothDevice.FromIdAsync(info.Id);
            if (device is not null)
            {
                device.ConnectionStatusChanged += OnConnectionStatusChanged;
                devices.Add(device);
            }
        }

        _devices = devices;
        StateChanged?.Invoke(State);
    }

    /// <summary>Asks Windows to connect the AirPods, then waits for them to actually connect.</summary>
    /// <returns>Whether they connected before the timeout.</returns>
    public async Task<bool> ConnectAsync(TimeSpan timeout)
    {
        var requested = await Task.Run(() =>
            BluetoothAudioEndpoint.Find(IsAirPods).Any(endpoint => endpoint.RequestConnect()));

        return requested && await WaitForStateAsync(AirPodsConnectionState.Connected, timeout);
    }

    /// <summary>Asks Windows to disconnect the AirPods, then waits for them to actually disconnect.</summary>
    /// <returns>Whether they disconnected before the timeout.</returns>
    public async Task<bool> DisconnectAsync(TimeSpan timeout)
    {
        // Every output is asked, because each one holds its own audio connection to the AirPods.
        var requests = await Task.Run(() =>
            BluetoothAudioEndpoint.Find(IsAirPods).Count(endpoint => endpoint.RequestDisconnect()));

        return requests > 0 && await WaitForStateAsync(AirPodsConnectionState.Disconnected, timeout);
    }

    /// <summary>Disconnects the AirPods, then connects them again.</summary>
    /// <returns>Whether they ended up connected.</returns>
    public async Task<bool> ReconnectAsync(TimeSpan timeout)
    {
        if (!await DisconnectAsync(timeout))
        {
            return false;
        }

        await Task.Delay(ReconnectPause);
        return await ConnectAsync(timeout);
    }

    private async Task<bool> WaitForStateAsync(AirPodsConnectionState state, TimeSpan timeout)
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

    public void Dispose() => ReleaseDevices();

    private void ReleaseDevices()
    {
        foreach (var device in _devices)
        {
            device.ConnectionStatusChanged -= OnConnectionStatusChanged;
            device.Dispose();
        }

        _devices = [];
    }

    private void OnConnectionStatusChanged(BluetoothDevice sender, object args) => StateChanged?.Invoke(State);
}

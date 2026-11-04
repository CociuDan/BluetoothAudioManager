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

        if (!requested)
        {
            return false;
        }

        var deadline = DateTimeOffset.Now + timeout;
        while (DateTimeOffset.Now < deadline)
        {
            if (State == AirPodsConnectionState.Connected)
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        return State == AirPodsConnectionState.Connected;
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

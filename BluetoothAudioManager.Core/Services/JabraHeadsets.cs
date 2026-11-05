using Jabra.NET.Sdk.Core;
using Jabra.NET.Sdk.Core.Types;
using Jabra.NET.Sdk.DevicePairing;
using Jabra.NET.Sdk.Properties;
using BluetoothAudioManager.Core.Models;

namespace BluetoothAudioManager.Core.Services;

/// <summary>
/// Reads Jabra headsets' names and battery levels, and connects them through their adapter, using Jabra's own kit.
/// The kit needs a partner key from Jabra. Changed is raised on a background thread.
/// </summary>
public sealed class JabraHeadsets : IDisposable
{
    private const string BatteryProperty = "batteryLevel";
    private const string ChargingProperty = "isCharging";

    private readonly Lock _lock = new();
    private readonly Dictionary<IDevice, JabraHeadsetStatus> _headsets = [];
    private readonly Dictionary<string, IBluetoothDongle> _adapters = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _lastHeadsetNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int?> _lastLevels = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IDisposable> _subscriptions = [];
    private BluetoothModule? _bluetooth;

    public event Action? Changed;

    /// <summary>Whether the kit started, so it can be trusted to report headsets coming and going.</summary>
    public bool IsRunning { get; private set; }

    public async Task StartAsync(string partnerKey)
    {
        var sdk = Init.InitManualSdk(new Config(
            partnerKey: partnerKey,
            appId: "BluetoothAudioManager",
            appName: "Bluetooth Audio Manager"));
        var properties = await new PropertyModule(sdk).CreatePropertyFactory();
        _bluetooth = new BluetoothModule(sdk);

        // Listening starts before the kit looks for devices, so none are missed.
        lock (_lock)
        {
            _subscriptions.Add(sdk.DeviceAdded.Subscribe(device => OnDeviceAdded(device, properties)));
            _subscriptions.Add(sdk.DeviceRemoved.Subscribe(OnDeviceRemoved));
        }

        await sdk.Start();
        IsRunning = true;
        Changed?.Invoke();
    }

    /// <summary>The headset connected through the named adapter, or the only headset found if the kit doesn't say which adapter it uses.</summary>
    /// <remarks>
    /// A headset charging from a USB cable also appears a second time, connected by USB, and can be removed and added again.
    /// So a missing level is taken from that second copy, or else is the last level the headset reported.
    /// </remarks>
    public JabraHeadsetStatus? FindByAdapter(string adapterName)
    {
        lock (_lock)
        {
            var headsets = _headsets.Values.ToList();
            var headset = headsets.FirstOrDefault(headset => string.Equals(headset.AdapterName, adapterName, StringComparison.OrdinalIgnoreCase))
                ?? (headsets.Count == 1 ? headsets[0] : null);
            if (headset is null)
            {
                return null;
            }

            var copies = headsets.Where(other => string.Equals(other.Name, headset.Name, StringComparison.OrdinalIgnoreCase)).ToList();
            var level = headset.BatteryPercent
                ?? copies.Select(other => other.BatteryPercent).FirstOrDefault(percent => percent is not null)
                ?? _lastLevels.GetValueOrDefault(headset.Name);
            return headset with { BatteryPercent = level, IsCharging = copies.Any(other => other.IsCharging) };
        }
    }

    /// <summary>The connected headset's name, or the last one seen on this adapter, so the name stays while the headset is off.</summary>
    public string? HeadsetNameFor(string adapterName)
    {
        if (FindByAdapter(adapterName)?.Name is { } name)
        {
            return name;
        }

        lock (_lock)
        {
            return _lastHeadsetNames.GetValueOrDefault(adapterName);
        }
    }

    /// <summary>Whether the kit can connect and disconnect headsets through this adapter.</summary>
    public bool CanConnect(string adapterName)
    {
        lock (_lock)
        {
            return _adapters.ContainsKey(adapterName);
        }
    }

    /// <summary>Asks the adapter to connect the headset paired with it, preferring the one last seen on it.</summary>
    /// <returns>Whether the adapter was asked, which doesn't mean the headset has connected yet.</returns>
    public async Task<bool> ConnectAsync(string adapterName, TimeSpan timeout)
    {
        if (AdapterFor(adapterName) is not { } adapter)
        {
            return false;
        }

        var paired = await adapter.GetPairingList();
        if (paired.Any(entry => entry.ConnectionStatus == BluetoothConnectionStatus.CONNECTED))
        {
            return true;
        }

        var lastName = HeadsetNameFor(adapterName);
        var headset = paired.FirstOrDefault(entry => string.Equals(entry.BluetoothName, lastName, StringComparison.OrdinalIgnoreCase))
            ?? paired.FirstOrDefault();
        if (headset is null)
        {
            return false;
        }

        await adapter.ConnectTo(headset, timeout);
        return true;
    }

    /// <summary>Asks the adapter to disconnect every headset connected to it.</summary>
    /// <returns>Whether the adapter was asked.</returns>
    public async Task<bool> DisconnectAsync(string adapterName)
    {
        if (AdapterFor(adapterName) is not { } adapter)
        {
            return false;
        }

        foreach (var entry in await adapter.GetPairingList())
        {
            if (entry.ConnectionStatus == BluetoothConnectionStatus.CONNECTED)
            {
                await adapter.DisconnectFrom(entry);
            }
        }

        return true;
    }

    private IBluetoothDongle? AdapterFor(string adapterName)
    {
        lock (_lock)
        {
            return _adapters.GetValueOrDefault(adapterName);
        }
    }

    private async void OnDeviceAdded(IDevice device, IPropertyFactory properties)
    {
        // Adapters the kit can control are kept for connecting, and aren't headsets themselves.
        try
        {
            if (_bluetooth is not null && await _bluetooth.CreateBluetoothDongle(device) is { } adapter)
            {
                lock (_lock)
                {
                    _adapters[device.Name] = adapter;
                }

                Changed?.Invoke();
                return;
            }
        }
        catch (Exception)
        {
            // Treated as a device that isn't a controllable adapter.
        }

        try
        {
            var subscription = device.ConnectionList.Subscribe(connections => SetAdapter(device, connections));
            lock (_lock)
            {
                _subscriptions.Add(subscription);
            }

            var battery = (await properties.CreateProperties(device, [BatteryProperty]))[BatteryProperty];

            // Watched before the first read, and kept if that read fails, as it can while the headset charges.
            var watch = battery.Watch.Subscribe(value => SetBattery(device, ToPercent(value)), _ => { });
            lock (_lock)
            {
                _subscriptions.Add(watch);
            }

            SetBattery(device, ToPercent(await battery.Get()));
        }
        catch (Exception)
        {
            // The adapter itself, and headsets that don't report a level, end up here and show no battery.
        }

        try
        {
            var charging = (await properties.CreateProperties(device, [ChargingProperty]))[ChargingProperty];
            var watch = charging.Watch.Subscribe(value => SetCharging(device, value is BooleanPropertyValue { Value: true }), _ => { });
            lock (_lock)
            {
                _subscriptions.Add(watch);
            }

            SetCharging(device, await charging.Get() is BooleanPropertyValue { Value: true });
        }
        catch (Exception)
        {
            // Headsets that don't say whether they're charging show no charging icon.
        }
    }

    private void SetCharging(IDevice device, bool isCharging)
    {
        lock (_lock)
        {
            // Only headsets already listed for their level or adapter are updated.
            if (_headsets.GetValueOrDefault(device) is not { } current || current.IsCharging == isCharging)
            {
                return;
            }

            _headsets[device] = current with { IsCharging = isCharging };
        }

        Changed?.Invoke();
    }

    private void OnDeviceRemoved(IDevice device)
    {
        lock (_lock)
        {
            _headsets.Remove(device);
            _adapters.Remove(device.Name);
        }

        Changed?.Invoke();
    }

    private void SetAdapter(IDevice device, List<IConnection> connections)
    {
        // A headset on a USB adapter has an indirect connection, whose parent is the adapter.
        var adapter = connections
            .FirstOrDefault(connection => connection.Type == ConnectionType.Indirect && connection.ParentConnection is not null)
            ?.ParentConnection?.Device.Name;

        lock (_lock)
        {
            // The adapter is a Jabra device too, connected directly, and isn't a headset to list.
            var current = _headsets.GetValueOrDefault(device);
            if (adapter is null && current is null)
            {
                return;
            }

            _headsets[device] = new JabraHeadsetStatus(device.Name, adapter, current?.BatteryPercent, current?.IsCharging == true);
            if (adapter is not null)
            {
                _lastHeadsetNames[adapter] = device.Name;
            }
        }

        Changed?.Invoke();
    }

    private void SetBattery(IDevice device, int? percent)
    {
        // Only devices that report a level are listed, which leaves out the adapter.
        // A reading without a level, which a charging headset can send, keeps the level it had.
        if (percent is null)
        {
            return;
        }

        lock (_lock)
        {
            var current = _headsets.GetValueOrDefault(device);
            _headsets[device] = new JabraHeadsetStatus(device.Name, current?.AdapterName, percent, current?.IsCharging == true);
            _lastLevels[device.Name] = percent;
        }

        Changed?.Invoke();
    }

    private static int? ToPercent(PropertyValue value) =>
        value is IntegerPropertyValue { Value: >= 0 and <= 100 } level ? level.Value : null;

    public void Dispose()
    {
        lock (_lock)
        {
            _subscriptions.ForEach(subscription => subscription.Dispose());
            _subscriptions.Clear();
        }
    }
}

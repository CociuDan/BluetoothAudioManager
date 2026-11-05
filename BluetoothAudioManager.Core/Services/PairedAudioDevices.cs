using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using BluetoothAudioManager.Core.Enums;
using BluetoothAudioManager.Core.Helpers;
using BluetoothAudioManager.Core.Models;

namespace BluetoothAudioManager.Core.Services;

/// <summary>Finds the audio devices paired with this PC, connected or not, and headsets on a USB adapter that's plugged in.</summary>
public static class PairedAudioDevices
{
    public const string AirPodsName = "AirPods";

    private const string AirPodsMaxName = "AirPods Max";
    private const string InstanceIdProperty = "System.Devices.DeviceInstanceId";

    public static bool IsAirPods(string deviceName) =>
        deviceName.Contains(AirPodsName, StringComparison.OrdinalIgnoreCase);

    public static bool IsAirPodsMax(string deviceName) =>
        deviceName.Contains(AirPodsMaxName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether the name belongs to AirPods with pods and a case. AirPods Max are left out,
    /// because they have one battery and are shown like other headphones.
    /// </summary>
    public static bool IsAirPodsWithPods(string deviceName) =>
        IsAirPods(deviceName) && !IsAirPodsMax(deviceName);

    /// <summary>The same check by name, also using the product ID to recognize AirPods Max.</summary>
    public static bool IsAirPodsWithPods(PairedAudioDevice device) =>
        IsAirPodsWithPods(device.Name) && AppleProducts.FamilyOf(device.AppleProductId) != AirPodsFamily.AirPodsMax;

    public static async Task<IReadOnlyList<PairedAudioDevice>> FindAsync()
    {
        var paired = await DeviceInformation.FindAllAsync(BluetoothDevice.GetDeviceSelectorFromPairingState(true));
        var devices = new List<PairedAudioDevice>();

        foreach (var info in paired)
        {
            using var device = await BluetoothDevice.FromIdAsync(info.Id);
            if (device?.ClassOfDevice.MajorClass == BluetoothMajorClass.AudioVideo
                && devices.All(found => found.Name != info.Name))
            {
                devices.Add(new PairedAudioDevice(info.Name, await ReadAppleProductIdAsync(device.BluetoothAddress)));
            }
        }

        try
        {
            foreach (var adapter in await Task.Run(() => UsbHeadsets.FindAdapterNames()))
            {
                devices.Add(new PairedAudioDevice(adapter, null, IsUsbAdapter: true));
            }
        }
        catch (Exception)
        {
            // The Bluetooth devices still appear if the sound outputs can't be read.
        }

        return devices;
    }

    // Windows names each Bluetooth service of a paired device with its address and, for Apple devices,
    // Apple's product ID. These stay after the device disconnects, while it remains paired.
    private static async Task<ushort?> ReadAppleProductIdAsync(ulong address)
    {
        try
        {
            var selector = $"{InstanceIdProperty}:~~\"{address:X12}\"";
            var services = await DeviceInformation.FindAllAsync(selector, [InstanceIdProperty], DeviceInformationKind.Device);

            foreach (var service in services)
            {
                var instanceId = service.Properties.TryGetValue(InstanceIdProperty, out var value) && value is string id ? id : service.Id;
                if (AppleProducts.ParseProductId(instanceId) is { } productId)
                {
                    return productId;
                }
            }
        }
        catch (Exception)
        {
            // Without the product ID, the device still appears, with the default pictures.
        }

        return null;
    }
}

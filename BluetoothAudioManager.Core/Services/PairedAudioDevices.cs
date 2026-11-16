using System.Globalization;
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

    // Windows keeps these for every pairing, so they can be read even while Bluetooth is off.
    private const string AddressProperty = "System.Devices.Aep.DeviceAddress";
    private const string MajorClassProperty = "System.Devices.Aep.Bluetooth.Cod.Major";

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

    /// <summary>
    /// The paired audio devices and plugged-in USB adapters. Paired devices are found from what Windows keeps for each
    /// pairing, so they're listed even while Bluetooth is off, such as when the app starts with it off.
    /// </summary>
    public static async Task<IReadOnlyList<PairedAudioDevice>> FindAsync()
    {
        var paired = await DeviceInformation.FindAllAsync(
            BluetoothDevice.GetDeviceSelectorFromPairingState(true),
            [AddressProperty, MajorClassProperty],
            DeviceInformationKind.AssociationEndpoint);
        var devices = new List<PairedAudioDevice>();

        foreach (var info in paired)
        {
            if (devices.All(found => found.Name != info.Name) && await AudioDeviceAddressAsync(info) is { } address)
            {
                devices.Add(new PairedAudioDevice(info.Name, await ReadAppleProductIdAsync(address)));
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

    /// <param name="text">A Bluetooth address as Windows writes it, such as "a1:b2:c3:d4:e5:f6".</param>
    /// <returns>The address as a number, or null if the text isn't one.</returns>
    public static ulong? ParseAddress(string? text)
    {
        var digits = (text ?? "").Replace(":", "").Replace("-", "");
        return digits.Length == 12 && ulong.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var address)
            ? address
            : null;
    }

    // The address of an audio device, or null for anything else. Read from what Windows keeps for the pairing, and only
    // if that's missing, from the device itself, which Windows can't open while Bluetooth is off.
    private static async Task<ulong?> AudioDeviceAddressAsync(DeviceInformation info)
    {
        if (info.Properties.TryGetValue(MajorClassProperty, out var major) && major is IConvertible
            && info.Properties.TryGetValue(AddressProperty, out var text) && ParseAddress(text as string) is { } address)
        {
            return Convert.ToInt32(major, CultureInfo.InvariantCulture) == (int)BluetoothMajorClass.AudioVideo ? address : null;
        }

        try
        {
            using var device = await BluetoothDevice.FromIdAsync(info.Id);
            return device?.ClassOfDevice.MajorClass == BluetoothMajorClass.AudioVideo ? device.BluetoothAddress : null;
        }
        catch (Exception)
        {
            // Left out until the next look, as before.
            return null;
        }
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

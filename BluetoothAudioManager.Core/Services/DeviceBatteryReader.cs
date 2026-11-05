using Windows.Devices.Enumeration;

namespace BluetoothAudioManager.Core.Services;

/// <summary>
/// Reads the battery level Windows stores for a Bluetooth device, if the device reports one.
/// Microsoft doesn't document this property, and many headphones never fill it in.
/// </summary>
public static class DeviceBatteryReader
{
    private const string BatteryProperty = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2";

    public static async Task<int?> ReadPercentAsync(string deviceName)
    {
        var selector = $"System.ItemNameDisplay:~~\"{deviceName.Replace("\"", "")}\"";
        var devices = await DeviceInformation.FindAllAsync(selector, [BatteryProperty], DeviceInformationKind.Device);

        foreach (var device in devices)
        {
            if (device.Properties.TryGetValue(BatteryProperty, out var value) && ParsePercent(value) is { } percent)
            {
                return percent;
            }
        }

        return null;
    }

    public static int? ParsePercent(object? value) =>
        value is byte or sbyte or short or ushort or int or uint && Convert.ToInt32(value) is >= 0 and <= 100
            ? Convert.ToInt32(value)
            : null;
}

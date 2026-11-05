using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace BluetoothAudioManager.Core.Services;

/// <summary>Finds the audio devices paired with this PC, connected or not.</summary>
public static class PairedAudioDevices
{
    public const string AirPodsName = "AirPods";

    public static bool IsAirPods(string deviceName) =>
        deviceName.Contains(AirPodsName, StringComparison.OrdinalIgnoreCase);

    public static async Task<IReadOnlyList<string>> FindNamesAsync()
    {
        var paired = await DeviceInformation.FindAllAsync(BluetoothDevice.GetDeviceSelectorFromPairingState(true));
        var names = new List<string>();

        foreach (var info in paired)
        {
            using var device = await BluetoothDevice.FromIdAsync(info.Id);
            if (device?.ClassOfDevice.MajorClass == BluetoothMajorClass.AudioVideo && !names.Contains(info.Name))
            {
                names.Add(info.Name);
            }
        }

        return names;
    }
}

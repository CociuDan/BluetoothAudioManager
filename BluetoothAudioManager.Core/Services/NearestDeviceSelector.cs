using BluetoothAudioManager.Core.Models;

namespace BluetoothAudioManager.Core.Services;

/// <summary>
/// Broadcasts aren't tied to a paired device and can come from anyone's AirPods,
/// so this picks the strongest signal heard recently, which is most likely the user's.
/// </summary>
public sealed class NearestDeviceSelector(TimeSpan window)
{
    private readonly Dictionary<ulong, AdvertisementSample> _latestByAddress = [];

    public AirPodsStatus Select(ulong address, short rssi, AirPodsStatus status, DateTimeOffset now)
    {
        _latestByAddress[address] = new AdvertisementSample(rssi, status, now);

        foreach (var (key, sample) in _latestByAddress)
        {
            if (now - sample.ReceivedAt > window)
            {
                _latestByAddress.Remove(key);
            }
        }

        return _latestByAddress.Values.MaxBy(sample => sample.Rssi)!.Status;
    }
}

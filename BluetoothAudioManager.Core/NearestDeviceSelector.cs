namespace BluetoothAudioManager.Core;

/// <summary>
/// Broadcasts aren't tied to a paired device and can come from anyone's AirPods,
/// so this picks the strongest signal heard recently, which is most likely the user's.
/// </summary>
public sealed class NearestDeviceSelector(TimeSpan window)
{
    private readonly Dictionary<ulong, Sample> _latestByAddress = [];

    public AirPodsStatus Select(ulong address, short rssi, AirPodsStatus status, DateTimeOffset now)
    {
        _latestByAddress[address] = new Sample(rssi, status, now);

        foreach (var (key, sample) in _latestByAddress)
        {
            if (now - sample.ReceivedAt > window)
            {
                _latestByAddress.Remove(key);
            }
        }

        return _latestByAddress.Values.MaxBy(sample => sample.Rssi)!.Status;
    }

    private sealed record Sample(short Rssi, AirPodsStatus Status, DateTimeOffset ReceivedAt);
}

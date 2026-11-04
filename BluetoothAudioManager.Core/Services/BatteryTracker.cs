namespace BluetoothAudioManager.Core;

/// <summary>
/// Keeps each battery's last reported level, because a part can briefly stop reporting,
/// such as a case whose last pod was just taken out.
/// </summary>
public sealed class BatteryTracker
{
    public AirPodsSnapshot? Current { get; private set; }

    public void Clear() => Current = null;

    public AirPodsSnapshot Update(AirPodsStatus status, DateTimeOffset now)
    {
        // A different model means different AirPods, whose old levels don't apply.
        var previous = Current?.Model == status.Model ? Current : null;

        Current = new AirPodsSnapshot(
            status.Model,
            Merge(status.Left, previous?.Left, now),
            Merge(status.Right, previous?.Right, now),
            Merge(status.Case, previous?.Case, now));

        return Current;
    }

    private static TrackedBattery Merge(BatteryReading reading, TrackedBattery? previous, DateTimeOffset now) =>
        reading.Percent is null
            ? previous ?? new TrackedBattery(null, false, null)
            : new TrackedBattery(reading.Percent, reading.IsCharging, now);
}

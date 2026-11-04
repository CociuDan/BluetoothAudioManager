namespace BluetoothAudioManager.Core;

/// <summary>A battery's latest known level. ReportedAt is null if it hasn't reported since the app started.</summary>
public sealed record TrackedBattery(int? Percent, bool IsCharging, DateTimeOffset? ReportedAt)
{
    public static readonly TimeSpan LevelExpiresAfter = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan ChargingExpiresAfter = TimeSpan.FromSeconds(15);

    /// <summary>The level to show now, or null once it's too old to trust.</summary>
    public int? PercentAt(DateTimeOffset now) =>
        ReportedAt is { } reportedAt && now - reportedAt <= LevelExpiresAfter ? Percent : null;

    public bool IsChargingAt(DateTimeOffset now) =>
        IsCharging && ReportedAt is { } reportedAt && now - reportedAt <= ChargingExpiresAfter;
}

public sealed record AirPodsSnapshot(
    string Model,
    TrackedBattery Left,
    TrackedBattery Right,
    TrackedBattery Case);

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

namespace BluetoothAudioManager.Core;

/// <summary>A battery's latest known level. ReportedAt is null if it hasn't reported since the app started.</summary>
public sealed record TrackedBattery(int? Percent, bool IsCharging, DateTimeOffset? ReportedAt)
{
    public static readonly TimeSpan FreshFor = TimeSpan.FromSeconds(15);

    /// <summary>The note shown under the level: its age once stale, or whether it's charging.</summary>
    public string DescribeDetail(DateTimeOffset now)
    {
        if (Percent is null || ReportedAt is not { } reportedAt)
        {
            return "Not reported yet";
        }

        var age = now - reportedAt;
        if (age > FreshFor)
        {
            return DescribeAge(age);
        }

        return IsCharging ? "Charging" : "";
    }

    public static string DescribeAge(TimeSpan age) =>
        age.TotalMinutes < 1 ? $"{(int)age.TotalSeconds} s ago"
        : age.TotalHours < 1 ? $"{(int)age.TotalMinutes} min ago"
        : $"{(int)age.TotalHours} h ago";
}

public sealed record AirPodsSnapshot(
    string Model,
    TrackedBattery Left,
    TrackedBattery Right,
    TrackedBattery Case,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Keeps each battery's last reported level, because a part that isn't reporting,
/// such as a case with no pods inside, still has the level it last sent.
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
            Merge(status.Case, previous?.Case, now),
            now);

        return Current;
    }

    private static TrackedBattery Merge(BatteryReading reading, TrackedBattery? previous, DateTimeOffset now) =>
        reading.Percent is null
            ? previous ?? new TrackedBattery(null, false, null)
            : new TrackedBattery(reading.Percent, reading.IsCharging, now);
}

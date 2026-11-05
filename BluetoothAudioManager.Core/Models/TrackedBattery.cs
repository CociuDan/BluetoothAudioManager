namespace BluetoothAudioManager.Core.Models;

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

using BluetoothAudioManager.Core;

namespace BluetoothAudioManager.Tests;

public class BatteryTrackerTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private static AirPodsStatus Status(int? casePercent, string model = "AirPods 4", bool caseCharging = false) =>
        new(model, new(90, false), new(80, false), new(casePercent, caseCharging));

    [Fact]
    public void CaseThatStopsReporting_KeepsItsLastLevelAndTime()
    {
        var tracker = new BatteryTracker();
        tracker.Update(Status(60), Start);

        var snapshot = tracker.Update(Status(null), Start.AddSeconds(30));

        Assert.Equal(60, snapshot.Case.PercentAt(Start.AddSeconds(30)));
        Assert.Equal(Start, snapshot.Case.ReportedAt);
        Assert.Equal(Start.AddSeconds(30), snapshot.Left.ReportedAt);
    }

    [Fact]
    public void CaseThatNeverReported_HasNoLevel()
    {
        var snapshot = new BatteryTracker().Update(Status(null), Start);

        Assert.Null(snapshot.Case.PercentAt(Start));
        Assert.Null(snapshot.Case.ReportedAt);
    }

    [Fact]
    public void CaseThatReportsAgain_IsUpdated()
    {
        var tracker = new BatteryTracker();
        tracker.Update(Status(60), Start);
        tracker.Update(Status(null), Start.AddMinutes(5));

        var snapshot = tracker.Update(Status(50), Start.AddMinutes(6));

        Assert.Equal(50, snapshot.Case.PercentAt(Start.AddMinutes(6)));
        Assert.Equal(Start.AddMinutes(6), snapshot.Case.ReportedAt);
    }

    [Fact]
    public void Clear_ForgetsLastLevels()
    {
        var tracker = new BatteryTracker();
        tracker.Update(Status(60), Start);

        tracker.Clear();
        var snapshot = tracker.Update(Status(null), Start.AddSeconds(1));

        Assert.Null(snapshot.Case.PercentAt(Start.AddSeconds(1)));
    }

    [Fact]
    public void DifferentModel_DoesNotInheritOldLevels()
    {
        var tracker = new BatteryTracker();
        tracker.Update(Status(60), Start);

        var snapshot = tracker.Update(Status(null, "AirPods Pro"), Start.AddSeconds(1));

        Assert.Null(snapshot.Case.PercentAt(Start.AddSeconds(1)));
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(60, 60)]
    [InlineData(61, null)]
    public void PercentAt_ExpiresAfterOneMinute(int seconds, int? expected)
    {
        var battery = new TrackedBattery(60, false, Start);

        Assert.Equal(expected, battery.PercentAt(Start.AddSeconds(seconds)));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(15, true)]
    [InlineData(16, false)]
    public void IsChargingAt_ExpiresAfterFifteenSeconds(int seconds, bool expected)
    {
        var battery = new TrackedBattery(60, true, Start);

        Assert.Equal(expected, battery.IsChargingAt(Start.AddSeconds(seconds)));
    }
}

using BluetoothAudioManager.Core;

namespace BluetoothAudioManager.Tests;

public class BatteryTrackerTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private static AirPodsStatus Status(int? casePercent, string model = "AirPods 4 (ANC)", bool caseCharging = false) =>
        new(model, new(90, false), new(80, false), new(casePercent, caseCharging));

    [Fact]
    public void CaseThatStopsReporting_KeepsItsLastLevelAndTime()
    {
        var tracker = new BatteryTracker();
        tracker.Update(Status(60), Start);

        var snapshot = tracker.Update(Status(null), Start.AddMinutes(5));

        Assert.Equal(60, snapshot.Case.Percent);
        Assert.Equal(Start, snapshot.Case.ReportedAt);
        Assert.Equal(Start.AddMinutes(5), snapshot.Left.ReportedAt);
    }

    [Fact]
    public void CaseThatNeverReported_HasNoLevel()
    {
        var snapshot = new BatteryTracker().Update(Status(null), Start);

        Assert.Null(snapshot.Case.Percent);
        Assert.Null(snapshot.Case.ReportedAt);
    }

    [Fact]
    public void CaseThatReportsAgain_IsUpdated()
    {
        var tracker = new BatteryTracker();
        tracker.Update(Status(60), Start);
        tracker.Update(Status(null), Start.AddMinutes(5));

        var snapshot = tracker.Update(Status(50), Start.AddMinutes(6));

        Assert.Equal(50, snapshot.Case.Percent);
        Assert.Equal(Start.AddMinutes(6), snapshot.Case.ReportedAt);
    }

    [Fact]
    public void Clear_ForgetsLastLevels()
    {
        var tracker = new BatteryTracker();
        tracker.Update(Status(60), Start);

        tracker.Clear();
        var snapshot = tracker.Update(Status(null), Start.AddSeconds(1));

        Assert.Null(snapshot.Case.Percent);
    }

    [Fact]
    public void DifferentModel_DoesNotInheritOldLevels()
    {
        var tracker = new BatteryTracker();
        tracker.Update(Status(60), Start);

        var snapshot = tracker.Update(Status(null, "AirPods Pro"), Start.AddSeconds(1));

        Assert.Null(snapshot.Case.Percent);
    }

    [Theory]
    [InlineData(0, "")]
    [InlineData(15, "")]
    [InlineData(16, "16 s ago")]
    [InlineData(300, "5 min ago")]
    [InlineData(7200, "2 h ago")]
    public void DescribeDetail_ShowsAgeOnceStale(int seconds, string expected)
    {
        var battery = new TrackedBattery(60, false, Start);

        Assert.Equal(expected, battery.DescribeDetail(Start.AddSeconds(seconds)));
    }

    [Fact]
    public void DescribeDetail_ShowsChargingOnlyWhileFresh()
    {
        var battery = new TrackedBattery(60, true, Start);

        Assert.Equal("Charging", battery.DescribeDetail(Start.AddSeconds(1)));
        Assert.Equal("1 min ago", battery.DescribeDetail(Start.AddMinutes(1)));
    }

    [Fact]
    public void DescribeDetail_ExplainsMissingLevel()
    {
        Assert.Equal("Not reported yet", new TrackedBattery(null, false, null).DescribeDetail(Start));
    }
}

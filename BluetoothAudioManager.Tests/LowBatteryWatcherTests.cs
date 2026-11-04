using BluetoothAudioManager.Core;

namespace BluetoothAudioManager.Tests;

public class LowBatteryWatcherTests
{
    private static AirPodsStatus Status(int? left, int? right = 90, int? casePercent = 90, bool leftCharging = false) =>
        new("AirPods 4", new(left, leftCharging), new(right, false), new(casePercent, false));

    [Fact]
    public void ReachingTheLevel_Alerts()
    {
        var alerts = new LowBatteryWatcher().Update(Status(20), 20);

        Assert.Equal(["Left pod is at 20%"], alerts);
    }

    [Fact]
    public void AboveTheLevel_DoesNotAlert()
    {
        Assert.Empty(new LowBatteryWatcher().Update(Status(30), 20));
    }

    [Fact]
    public void StayingLow_AlertsOnlyOnce()
    {
        var watcher = new LowBatteryWatcher();
        watcher.Update(Status(20), 20);

        Assert.Empty(watcher.Update(Status(20), 20));
        Assert.Empty(watcher.Update(Status(10), 20));
    }

    [Fact]
    public void ChargingBackAbove_AlertsAgainNextTime()
    {
        var watcher = new LowBatteryWatcher();
        watcher.Update(Status(20), 20);
        watcher.Update(Status(30), 20);

        Assert.Equal(["Left pod is at 20%"], watcher.Update(Status(20), 20));
    }

    [Fact]
    public void ChargingPart_DoesNotAlert()
    {
        Assert.Empty(new LowBatteryWatcher().Update(Status(10, leftCharging: true), 20));
    }

    [Fact]
    public void EachPart_IsWatchedSeparately()
    {
        var alerts = new LowBatteryWatcher().Update(Status(20, 10, 0), 20);

        Assert.Equal(["Left pod is at 20%", "Right pod is at 10%", "Case is at 0%"], alerts);
    }

    [Fact]
    public void PartNotReporting_IsIgnored()
    {
        Assert.Empty(new LowBatteryWatcher().Update(Status(null, 90, null), 20));
    }

    [Fact]
    public void UpdatePart_AlertsOnceForASingleDevice()
    {
        var watcher = new LowBatteryWatcher();

        Assert.Equal("Headset is at 20%", watcher.UpdatePart("Headset", new(20, false), 20));
        Assert.Null(watcher.UpdatePart("Headset", new(15, false), 20));
    }

    [Fact]
    public void Reset_AllowsANewAlert()
    {
        var watcher = new LowBatteryWatcher();
        watcher.Update(Status(20), 20);

        watcher.Reset();

        Assert.Equal(["Left pod is at 20%"], watcher.Update(Status(20), 20));
    }
}

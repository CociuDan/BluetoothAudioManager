using BluetoothAudioManager.Core;

namespace BluetoothAudioManager.Tests;

public class AirPodsConnectionMonitorTests
{
    [Theory]
    [InlineData("AirPods", true)]
    [InlineData("Dan's AirPods Pro", true)]
    [InlineData("AIRPODS", true)]
    [InlineData("MAJOR IV", false)]
    [InlineData("ASUS USB-BT500", false)]
    public void IsAirPods_MatchesByName(string name, bool expected)
    {
        Assert.Equal(expected, AirPodsConnectionMonitor.IsAirPods(name));
    }
}

using BluetoothAudioManager.Core;

namespace BluetoothAudioManager.Tests;

public class DeviceConnectionMonitorTests
{
    [Theory]
    [InlineData("AirPods", "AirPods", true)]
    [InlineData("AirPods", "Dan's AirPods Pro", true)]
    [InlineData("AirPods", "Headphones (AIRPODS Stereo)", true)]
    [InlineData("AirPods", "MAJOR IV", false)]
    [InlineData("MAJOR IV", "Headphones (MAJOR IV Stereo)", true)]
    [InlineData("MAJOR IV", "AirPods", false)]
    public void Matches_UsesTheDeviceName(string deviceName, string name, bool expected)
    {
        using var monitor = new DeviceConnectionMonitor(deviceName);

        Assert.Equal(expected, monitor.Matches(name));
    }
}

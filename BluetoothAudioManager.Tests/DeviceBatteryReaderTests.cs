using BluetoothAudioManager.Core;

namespace BluetoothAudioManager.Tests;

public class DeviceBatteryReaderTests
{
    [Fact]
    public void ParsePercent_AcceptsLevelsInRange()
    {
        Assert.Equal(0, DeviceBatteryReader.ParsePercent((byte)0));
        Assert.Equal(75, DeviceBatteryReader.ParsePercent((byte)75));
        Assert.Equal(100, DeviceBatteryReader.ParsePercent(100));
    }

    [Fact]
    public void ParsePercent_RejectsAnythingElse()
    {
        Assert.Null(DeviceBatteryReader.ParsePercent(null));
        Assert.Null(DeviceBatteryReader.ParsePercent((byte)101));
        Assert.Null(DeviceBatteryReader.ParsePercent(-1));
        Assert.Null(DeviceBatteryReader.ParsePercent("75"));
    }
}

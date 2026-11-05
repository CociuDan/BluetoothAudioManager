using BluetoothAudioManager.Core.Enums;
using BluetoothAudioManager.Core.Helpers;

namespace BluetoothAudioManager.Tests.Helpers;

public class BatteryBandTests
{
    [Theory]
    [InlineData(0, BatteryBand.Low)]
    [InlineData(9, BatteryBand.Low)]
    [InlineData(10, BatteryBand.Warning)]
    [InlineData(20, BatteryBand.Warning)]
    [InlineData(21, BatteryBand.Good)]
    [InlineData(100, BatteryBand.Good)]
    public void FromPercent_UsesTheColorThresholds(int percent, BatteryBand expected)
    {
        Assert.Equal(expected, BatteryBands.FromPercent(percent));
    }
}

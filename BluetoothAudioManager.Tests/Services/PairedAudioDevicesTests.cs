using BluetoothAudioManager.Core.Services;

namespace BluetoothAudioManager.Tests.Services;

public class PairedAudioDevicesTests
{
    [Theory]
    [InlineData("AirPods", true)]
    [InlineData("Dan's AirPods Pro", true)]
    [InlineData("AIRPODS - Find My", true)]
    [InlineData("MAJOR IV", false)]
    public void IsAirPods_MatchesByName(string name, bool expected)
    {
        Assert.Equal(expected, PairedAudioDevices.IsAirPods(name));
    }
}

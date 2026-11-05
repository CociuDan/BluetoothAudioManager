using BluetoothAudioManager.Core.Models;
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

    [Theory]
    [InlineData("AirPods", true)]
    [InlineData("Dan's AirPods Pro", true)]
    [InlineData("Headphones (AirPods Stereo)", true)]
    [InlineData("AirPods Max", false)]
    [InlineData("Headphones (AirPods Max Stereo)", false)]
    [InlineData("MAJOR IV", false)]
    public void IsAirPodsWithPods_LeavesOutAirPodsMax(string name, bool expected)
    {
        Assert.Equal(expected, PairedAudioDevices.IsAirPodsWithPods(name));
    }

    [Theory]
    [InlineData((ushort)0x201B, true)]
    [InlineData((ushort)0x201F, false)]
    public void IsAirPodsWithPods_UsesTheProductIdForAirPodsMax(ushort productId, bool expected)
    {
        Assert.Equal(expected, PairedAudioDevices.IsAirPodsWithPods(new PairedAudioDevice("AirPods", productId)));
    }
}

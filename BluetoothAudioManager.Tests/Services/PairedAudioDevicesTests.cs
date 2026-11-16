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

    [Theory]
    [InlineData("c0:b2:2f:6c:69:c6", 0xC0B22F6C69C6UL)]
    [InlineData("C0-B2-2F-6C-69-C6", 0xC0B22F6C69C6UL)]
    [InlineData("00:00:00:00:00:01", 0x1UL)]
    public void ParseAddress_ReadsTheAddressWindowsKeeps(string text, ulong expected)
    {
        Assert.Equal(expected, PairedAudioDevices.ParseAddress(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("c0:b2:2f:6c:69")]
    [InlineData("not an address")]
    public void ParseAddress_AnythingElse_IsNull(string? text)
    {
        Assert.Null(PairedAudioDevices.ParseAddress(text));
    }
}

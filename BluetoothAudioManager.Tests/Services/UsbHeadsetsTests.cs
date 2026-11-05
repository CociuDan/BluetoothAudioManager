using BluetoothAudioManager.Core.Enums;
using BluetoothAudioManager.Core.Services;

namespace BluetoothAudioManager.Tests.Services;

public class UsbHeadsetsTests
{
    [Theory]
    [InlineData("Speakers (Jabra Link 370)", "Jabra Link 370")]
    [InlineData("Speakers (2- Jabra Link 380)", "Jabra Link 380")]
    [InlineData("Headset (JABRA LINK 390)", "JABRA LINK 390")]
    public void AdapterNameFrom_FindsJabraAdapters(string outputName, string expected)
    {
        Assert.Equal(expected, UsbHeadsets.AdapterNameFrom(outputName));
    }

    [Theory]
    [InlineData("Headphones (AirPods Stereo)")]
    [InlineData("SPDIF Interface (3- FiiO USB DAC-E10)")]
    [InlineData("Speakers (Jabra Evolve 65)")]
    public void AdapterNameFrom_IgnoresOtherOutputs(string outputName)
    {
        Assert.Null(UsbHeadsets.AdapterNameFrom(outputName));
    }

    [Fact]
    public void Monitor_CantConnectAndMatchesItsAdapter()
    {
        using var monitor = new UsbHeadsetMonitor("Jabra Link 370");

        Assert.False(monitor.CanConnect);
        Assert.Equal(DeviceConnectionState.NotPaired, monitor.State);
        Assert.True(monitor.Matches("Speakers (Jabra Link 370)"));
        Assert.False(monitor.Matches("Headphones (AirPods Stereo)"));
    }
}

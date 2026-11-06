using BluetoothAudioManager.Shared.Helpers;

namespace BluetoothAudioManager.Tests.Shared;

public class VersionsTests
{
    [Theory]
    [InlineData("v0.0.3", "0.0.2", true)]
    [InlineData("0.1.0", "0.0.9", true)]
    [InlineData("v1.0.0", "0.10.10", true)]
    [InlineData("v0.0.10", "0.0.9", true)]
    [InlineData("v0.0.3", "0.0.3", false)]
    [InlineData("v0.0.2", "0.0.3", false)]
    [InlineData("v0.0.3", null, true)]
    [InlineData("v0.0.3", "unknown", true)]
    [InlineData("not a version", "0.0.1", false)]
    [InlineData("v1.2", "0.0.1", false)]
    public void IsNewer_ComparesReleaseWithInstalled(string release, string? installed, bool expected)
    {
        Assert.Equal(expected, Versions.IsNewer(release, installed));
    }
}

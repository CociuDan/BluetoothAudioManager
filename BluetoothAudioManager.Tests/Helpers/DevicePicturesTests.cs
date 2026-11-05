using BluetoothAudioManager.Core.Helpers;

namespace BluetoothAudioManager.Tests.Helpers;

public class DevicePicturesTests
{
    [Theory]
    [InlineData("MAJOR IV", "MajorIV.png")]
    [InlineData("Marshall Major IV", "MajorIV.png")]
    [InlineData("AirPods Max", "AirPodsMax.png")]
    [InlineData("Dan's AirPods Max", "AirPodsMax.png")]
    [InlineData("Jabra Evolve 65", "JabraEvolve65.png")]
    [InlineData("Jabra Link 370", "JabraEvolve65.png")]
    public void FileFor_FindsThePictureByName(string deviceName, string expected)
    {
        Assert.Equal(expected, DevicePictures.FileFor(deviceName));
    }

    [Theory]
    [InlineData("WH-1000XM5")]
    [InlineData("Jabra Link 380")]
    [InlineData("")]
    public void FileFor_WithoutAPicture_IsNull(string deviceName)
    {
        Assert.Null(DevicePictures.FileFor(deviceName));
    }
}

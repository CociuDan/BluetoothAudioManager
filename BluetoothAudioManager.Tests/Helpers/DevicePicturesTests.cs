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
    [InlineData("MAJOR II BLUETOOTH", "MajorII.png")]
    [InlineData("MAJOR III BLUETOOTH", "MajorIII.png")]
    [InlineData("Major III Bluetooth", "MajorIII.png")]
    [InlineData("MAJOR V", "MajorV.png")]
    [InlineData("MID", "Mid.png")]
    [InlineData("MID ANC", "MidANC.png")]
    [InlineData("MID A.N.C.", "MidANC.png")]
    [InlineData("MILTON A.N.C.", "MiltonANC.png")]
    [InlineData("Monitor Bluetooth", "Monitor.png")]
    [InlineData("MONITOR II", "MonitorIIANC.png")]
    [InlineData("Monitor II A.N.C.", "MonitorIIANC.png")]
    [InlineData("Monitor III A.N.C.", "MonitorIIIANC.png")]
    public void FileFor_TellsSimilarMarshallNamesApart(string deviceName, string expected)
    {
        Assert.Equal(expected, DevicePictures.FileFor(deviceName));
    }

    [Theory]
    [InlineData("Minor II", "MinorII", false)]
    [InlineData("MINOR III", "MinorIII", true)]
    [InlineData("MINOR IV", "MinorIV", true)]
    [InlineData("Minor IV [LE]", "MinorIV", true)]
    [InlineData("MODE II", "ModeII", true)]
    [InlineData("MOTIF A.N.C.", "MotifANC", true)]
    [InlineData("MOTIF II A.N.C.", "MotifIIANC", true)]
    public void For_Earbuds_GivesBudsAndCase(string deviceName, string model, bool hasCase)
    {
        var picture = DevicePictures.For(deviceName);

        Assert.NotNull(picture);
        Assert.True(picture.IsEarbuds);
        Assert.Null(picture.FileName);
        Assert.Equal($"{model}-L.png", picture.LeftBud);
        Assert.Equal($"{model}-R.png", picture.RightBud);
        Assert.Equal(hasCase ? $"{model}-case.png" : null, picture.Case);
    }

    [Theory]
    [InlineData("WH-1000XM5")]
    [InlineData("Jabra Link 380")]
    [InlineData("Midnight Buds")]
    [InlineData("MONITOR")]
    [InlineData("")]
    public void FileFor_WithoutAPicture_IsNull(string deviceName)
    {
        Assert.Null(DevicePictures.FileFor(deviceName));
    }
}

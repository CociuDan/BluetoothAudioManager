using BluetoothAudioManager.Core.Helpers;

namespace BluetoothAudioManager.Tests.Helpers;

public class DevicePicturesTests
{
    [Theory]
    [InlineData("AirPods Max", "Apple")]
    [InlineData("MAJOR IV", "Marshall")]
    [InlineData("LinkBuds", "Sony")]
    [InlineData("Jabra Evolve 65", "Jabra")]
    public void For_AssociatesPictureWithBrandFolder(string deviceName, string expectedFolder)
    {
        Assert.Equal(expectedFolder, DevicePictures.For(deviceName)?.AssetFolder);
    }

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
    [InlineData("WH-1000XM4", "WH1000XM4.png")]
    [InlineData("LE_WH-1000XM4", "WH1000XM4.png")]
    [InlineData("WH-1000XM4C", "WH1000XM4C.png")]
    [InlineData("WH-1000XM5", "WH1000XM5.png")]
    [InlineData("1000X THE COLLEXION", "Sony1000XTheCollexion.png")]
    [InlineData("h.ear (MDR-100ABN)", "MDR100ABN.png")]
    [InlineData("WH-H910N (h.ear)", "WHH910N.png")]
    [InlineData("LE_WH-H810 (h.ear)", "WHH810.png")]
    [InlineData("WH-CH735N", "WHCH730N.png")]
    [InlineData("ULT WEAR", "ULTWear.png")]
    [InlineData("INZONE H9", "InzoneH9.png")]
    [InlineData("INZONE H9 II", "InzoneH9II.png")]
    public void FileFor_FindsSonyHeadphones(string deviceName, string expected)
    {
        Assert.Equal(expected, DevicePictures.FileFor(deviceName));
    }

    [Theory]
    [InlineData("WF-1000XM4", "WF1000XM4")]
    [InlineData("LE_WF-SP800N", "WFSP800N")]
    [InlineData("WF-H800 (h.ear)", "WFH800")]
    [InlineData("LinkBuds", "LinkBuds")]
    [InlineData("LinkBuds S", "LinkBudsS")]
    [InlineData("LinkBuds Fit", "LinkBudsFit")]
    [InlineData("INZONE Buds", "InzoneBuds")]
    public void For_SonyEarbuds_GivesBudsAndCase(string deviceName, string model)
    {
        var picture = DevicePictures.For(deviceName);

        Assert.NotNull(picture);
        Assert.True(picture.IsEarbuds);
        Assert.Equal($"{model}-L.png", picture.LeftBud);
        Assert.Equal($"{model}-case.png", picture.Case);
    }

    [Theory]
    [InlineData("WH-1000XM9")]
    [InlineData("LE_")]
    [InlineData("Jabra Link 380")]
    [InlineData("Midnight Buds")]
    [InlineData("MONITOR")]
    [InlineData("")]
    public void FileFor_WithoutAPicture_IsNull(string deviceName)
    {
        Assert.Null(DevicePictures.FileFor(deviceName));
    }
}

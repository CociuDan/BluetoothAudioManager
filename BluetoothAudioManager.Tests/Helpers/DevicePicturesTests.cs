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
    [InlineData("AirPods Max", "AirPodsMaxMidnight.png")]
    [InlineData("Dan's AirPods Max", "AirPodsMaxMidnight.png")]
    [InlineData("Jabra Evolve 65", "JabraEvolve65.png")]
    [InlineData("Jabra Evolve 65 Mono", "JabraEvolve65Mono.png")]
    [InlineData("Jabra Evolve2 65 Flex", "JabraEvolve2_65Flex.png")]
    [InlineData("Jabra Link 370", "JabraEvolve65.png")]
    public void FileFor_FindsThePictureByName(string deviceName, string expected)
    {
        Assert.Equal(expected, DevicePictures.FileFor(deviceName));
    }

    [Theory]
    [InlineData((ushort)0x200A, false, "AirPodsMaxSpaceGray.png")]
    [InlineData((ushort)0x200A, true, "AirPodsMaxSilver.png")]
    [InlineData((ushort)0x201F, false, "AirPodsMaxMidnight.png")]
    [InlineData((ushort)0x201F, true, "AirPodsMaxStarlight.png")]
    [InlineData(null, false, "AirPodsMaxMidnight.png")]
    [InlineData(null, true, "AirPodsMaxStarlight.png")]
    public void AirPodsMaxFileFor_ChoosesTheColorByVersionAndTheme(ushort? productId, bool lightTheme, string expected)
    {
        Assert.Equal(expected, DevicePictures.AirPodsMaxFileFor(productId, lightTheme));
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
    [InlineData("MDR-1000X", "MDR1000X.png")]
    [InlineData("WH-1000XM2", "WH1000XM2.png")]
    [InlineData("WH-1000XM3", "WH1000XM3.png")]
    [InlineData("WH-1000XM4", "WH1000XM4.png")]
    [InlineData("LE_WH-1000XM4", "WH1000XM4.png")]
    [InlineData("WH-1000XM4C", "WH1000XM4C.png")]
    [InlineData("WH-1000XM5", "WH1000XM5.png")]
    [InlineData("WH-1000XM6", "WH1000XM6.png")]
    [InlineData("1000X THE COLLEXION", "Sony1000XTheCollexion.png")]
    [InlineData("h.ear (MDR-100ABN)", "MDR100ABN.png")]
    [InlineData("WH-H800", "WHH800.png")]
    [InlineData("WH-H900N", "WHH900N.png")]
    [InlineData("WH-H910N (h.ear)", "WHH910N.png")]
    [InlineData("LE_WH-H810 (h.ear)", "WHH810.png")]
    [InlineData("WH-CH400", "WHCH400.png")]
    [InlineData("WH-CH500", "WHCH500.png")]
    [InlineData("WH-CH510", "WHCH510.png")]
    [InlineData("WH-CH520", "WHCH520.png")]
    [InlineData("WH-CH530", "WHCH530.png")]
    [InlineData("WH-CH700N", "WHCH700N.png")]
    [InlineData("WH-CH710N", "WHCH710N.png")]
    [InlineData("WH-CH720N", "WHCH720N.png")]
    [InlineData("WH-CH730N", "WHCH730N.png")]
    [InlineData("WH-CH735N", "WHCH730N.png")]
    [InlineData("MDR-XB650BT", "MDRXB650BT.png")]
    [InlineData("MDR-XB950B1", "MDRXB950B1.png")]
    [InlineData("MDR-XB950N1", "MDRXB950N1.png")]
    [InlineData("WH-XB700", "WHXB700.png")]
    [InlineData("WH-XB900N", "WHXB900N.png")]
    [InlineData("WH-XB910N", "WHXB910N.png")]
    [InlineData("MDR-ZX220BT", "MDRZX220BT.png")]
    [InlineData("ULT WEAR", "ULTWear.png")]
    [InlineData("INZONE H7", "InzoneH7.png")]
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

using BluetoothAudioManager.Core.Models;

namespace BluetoothAudioManager.Core.Helpers;

/// <summary>
/// Which pictures in the app's Assets folder show a device, found by its name.
/// To add a device, add its pictures to Assets and a line here.
/// </summary>
public static class DevicePictures
{
    private const string LowEnergyPrefix = "LE_";

    // A device matches when its name contains the name part as whole words, ignoring capitals and dots,
    // so "MID" matches "MID" but not "MID ANC", and "MONITOR II" doesn't match "MONITOR III".
    private static readonly (string NamePart, DevicePicture Picture)[] ByName =
    [
        ("AirPods Max", DevicePicture.Headphones("Apple", "AirPodsMax.png")),

        // Marshall headphones, by the names Windows shows for them.
        ("MAJOR II", DevicePicture.Headphones("Marshall", "MajorII.png")),
        ("MAJOR III", DevicePicture.Headphones("Marshall", "MajorIII.png")),
        ("MAJOR IV", DevicePicture.Headphones("Marshall", "MajorIV.png")),
        ("MAJOR V", DevicePicture.Headphones("Marshall", "MajorV.png")),
        ("MID", DevicePicture.Headphones("Marshall", "Mid.png")),
        ("MID ANC", DevicePicture.Headphones("Marshall", "MidANC.png")),
        ("MILTON ANC", DevicePicture.Headphones("Marshall", "MiltonANC.png")),
        ("MONITOR BLUETOOTH", DevicePicture.Headphones("Marshall", "Monitor.png")),
        ("MONITOR II", DevicePicture.Headphones("Marshall", "MonitorIIANC.png")),
        ("MONITOR III", DevicePicture.Headphones("Marshall", "MonitorIIIANC.png")),

        // Marshall earbuds. The Minor II's buds hang on a cable and have no case.
        ("MINOR II", DevicePicture.Earbuds("Marshall", "MinorII", hasCase: false)),
        ("MINOR III", DevicePicture.Earbuds("Marshall", "MinorIII")),
        ("MINOR IV", DevicePicture.Earbuds("Marshall", "MinorIV")),
        ("MODE II", DevicePicture.Earbuds("Marshall", "ModeII")),
        ("MOTIF ANC", DevicePicture.Earbuds("Marshall", "MotifANC")),
        ("MOTIF II ANC", DevicePicture.Earbuds("Marshall", "MotifIIANC")),

        // Sony headphones from 2016 on, by the names Windows shows for them. h.ear models add " (h.ear)", and the
        // MDR-100ABN shows "h.ear (MDR-100ABN)", which the model number matches either way.
        ("MDR-1000X", DevicePicture.Headphones("Sony", "MDR1000X.png")),
        ("WH-1000XM2", DevicePicture.Headphones("Sony", "WH1000XM2.png")),
        ("WH-1000XM3", DevicePicture.Headphones("Sony", "WH1000XM3.png")),
        ("WH-1000XM4", DevicePicture.Headphones("Sony", "WH1000XM4.png")),
        ("WH-1000XM4C", DevicePicture.Headphones("Sony", "WH1000XM4C.png")),
        ("WH-1000XM5", DevicePicture.Headphones("Sony", "WH1000XM5.png")),
        ("WH-1000XM6", DevicePicture.Headphones("Sony", "WH1000XM6.png")),
        ("1000X THE COLLEXION", DevicePicture.Headphones("Sony", "Sony1000XTheCollexion.png")),
        ("MDR-100ABN", DevicePicture.Headphones("Sony", "MDR100ABN.png")),
        ("WH-H800", DevicePicture.Headphones("Sony", "WHH800.png")),
        ("WH-H810", DevicePicture.Headphones("Sony", "WHH810.png")),
        ("WH-H900N", DevicePicture.Headphones("Sony", "WHH900N.png")),
        ("WH-H910N", DevicePicture.Headphones("Sony", "WHH910N.png")),
        ("WH-CH400", DevicePicture.Headphones("Sony", "WHCH400.png")),
        ("WH-CH500", DevicePicture.Headphones("Sony", "WHCH500.png")),
        ("WH-CH510", DevicePicture.Headphones("Sony", "WHCH510.png")),
        ("WH-CH520", DevicePicture.Headphones("Sony", "WHCH520.png")),
        ("WH-CH530", DevicePicture.Headphones("Sony", "WHCH530.png")),
        ("WH-CH700N", DevicePicture.Headphones("Sony", "WHCH700N.png")),
        ("WH-CH710N", DevicePicture.Headphones("Sony", "WHCH710N.png")),
        ("WH-CH720N", DevicePicture.Headphones("Sony", "WHCH720N.png")),
        ("WH-CH730N", DevicePicture.Headphones("Sony", "WHCH730N.png")),

        // Sold alongside the WH-CH730N and looks the same.
        ("WH-CH735N", DevicePicture.Headphones("Sony", "WHCH730N.png")),
        ("MDR-XB650BT", DevicePicture.Headphones("Sony", "MDRXB650BT.png")),
        ("MDR-XB950B1", DevicePicture.Headphones("Sony", "MDRXB950B1.png")),
        ("MDR-XB950N1", DevicePicture.Headphones("Sony", "MDRXB950N1.png")),
        ("WH-XB700", DevicePicture.Headphones("Sony", "WHXB700.png")),
        ("WH-XB900N", DevicePicture.Headphones("Sony", "WHXB900N.png")),
        ("WH-XB910N", DevicePicture.Headphones("Sony", "WHXB910N.png")),
        ("MDR-ZX220BT", DevicePicture.Headphones("Sony", "MDRZX220BT.png")),
        ("ULT WEAR", DevicePicture.Headphones("Sony", "ULTWear.png")),
        ("INZONE H7", DevicePicture.Headphones("Sony", "InzoneH7.png")),
        ("INZONE H9", DevicePicture.Headphones("Sony", "InzoneH9.png")),
        ("INZONE H9 II", DevicePicture.Headphones("Sony", "InzoneH9II.png")),

        // Sony earbuds.
        ("WF-1000X", DevicePicture.Earbuds("Sony", "WF1000X")),
        ("WF-1000XM3", DevicePicture.Earbuds("Sony", "WF1000XM3")),
        ("WF-1000XM4", DevicePicture.Earbuds("Sony", "WF1000XM4")),
        ("WF-1000XM5", DevicePicture.Earbuds("Sony", "WF1000XM5")),
        ("WF-1000XM6", DevicePicture.Earbuds("Sony", "WF1000XM6")),
        ("WF-C500", DevicePicture.Earbuds("Sony", "WFC500")),
        ("WF-C510", DevicePicture.Earbuds("Sony", "WFC510")),
        ("WF-C700N", DevicePicture.Earbuds("Sony", "WFC700N")),
        ("WF-C710N", DevicePicture.Earbuds("Sony", "WFC710N")),
        ("WF-H800", DevicePicture.Earbuds("Sony", "WFH800")),
        ("WF-SP700N", DevicePicture.Earbuds("Sony", "WFSP700N")),
        ("WF-SP800N", DevicePicture.Earbuds("Sony", "WFSP800N")),
        ("WF-SP900", DevicePicture.Earbuds("Sony", "WFSP900")),
        ("WF-XB700", DevicePicture.Earbuds("Sony", "WFXB700")),
        ("LinkBuds", DevicePicture.Earbuds("Sony", "LinkBuds")),
        ("LinkBuds S", DevicePicture.Earbuds("Sony", "LinkBudsS")),
        ("LinkBuds Fit", DevicePicture.Earbuds("Sony", "LinkBudsFit")),
        ("LinkBuds Open", DevicePicture.Earbuds("Sony", "LinkBudsOpen")),
        ("LinkBuds Clip", DevicePicture.Earbuds("Sony", "LinkBudsClip")),
        ("INZONE Buds", DevicePicture.Earbuds("Sony", "InzoneBuds")),

        ("Jabra Evolve 65", DevicePicture.Headphones("Jabra", "JabraEvolve65.png")),

        // The adapter's name stands in for its headset until Jabra's kit names it, and it comes with the Evolve 65.
        ("Jabra Link 370", DevicePicture.Headphones("Jabra", "JabraEvolve65.png")),
    ];

    /// <returns>The device's pictures, or null if there are none and a placeholder should be shown.</returns>
    public static DevicePicture? For(string deviceName)
    {
        var name = $" {Normalize(deviceName)} ";

        // The longest matching name part wins, so a more specific name is never hidden by a shorter one.
        return ByName
            .Where(entry => name.Contains($" {Normalize(entry.NamePart)} ", StringComparison.Ordinal))
            .OrderByDescending(entry => entry.NamePart.Length)
            .Select(entry => entry.Picture)
            .FirstOrDefault();
    }

    /// <returns>The single picture's file name, or null for earbuds and devices without a picture.</returns>
    public static string? FileFor(string deviceName) => For(deviceName)?.FileName;

    // Capitals and dots don't matter, brackets separate words like spaces, and runs of spaces count as one.
    // Some headphones also appear a second time for Bluetooth LE with "LE_" before their name, such as
    // "LE_WH-1000XM4", which matches like the name without it.
    private static string Normalize(string name) =>
        string.Join(' ', name.Replace(".", "").ToUpperInvariant()
            .Split([' ', '(', ')', '[', ']'], StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.StartsWith(LowEnergyPrefix, StringComparison.Ordinal) && word.Length > LowEnergyPrefix.Length
                ? word[LowEnergyPrefix.Length..]
                : word));
}

using BluetoothAudioManager.Core.Models;

namespace BluetoothAudioManager.Core.Helpers;

/// <summary>
/// Which pictures in the app's Assets folder show a device, found by its name.
/// To add a device, add its pictures to Assets and a line here.
/// </summary>
public static class DevicePictures
{
    // A device matches when its name contains the name part as whole words, ignoring capitals and dots,
    // so "MID" matches "MID" but not "MID ANC", and "MONITOR II" doesn't match "MONITOR III".
    private static readonly (string NamePart, DevicePicture Picture)[] ByName =
    [
        ("AirPods Max", DevicePicture.Headphones("AirPodsMax.png")),

        // Marshall headphones, by the names Windows shows for them.
        ("MAJOR II", DevicePicture.Headphones("MajorII.png")),
        ("MAJOR III", DevicePicture.Headphones("MajorIII.png")),
        ("MAJOR IV", DevicePicture.Headphones("MajorIV.png")),
        ("MAJOR V", DevicePicture.Headphones("MajorV.png")),
        ("MID", DevicePicture.Headphones("Mid.png")),
        ("MID ANC", DevicePicture.Headphones("MidANC.png")),
        ("MILTON ANC", DevicePicture.Headphones("MiltonANC.png")),
        ("MONITOR BLUETOOTH", DevicePicture.Headphones("Monitor.png")),
        ("MONITOR II", DevicePicture.Headphones("MonitorIIANC.png")),
        ("MONITOR III", DevicePicture.Headphones("MonitorIIIANC.png")),

        // Marshall earbuds. The Minor II's buds hang on a cable and have no case.
        ("MINOR II", DevicePicture.Earbuds("MinorII", hasCase: false)),
        ("MINOR III", DevicePicture.Earbuds("MinorIII")),
        ("MINOR IV", DevicePicture.Earbuds("MinorIV")),
        ("MODE II", DevicePicture.Earbuds("ModeII")),
        ("MOTIF ANC", DevicePicture.Earbuds("MotifANC")),
        ("MOTIF II ANC", DevicePicture.Earbuds("MotifIIANC")),

        ("Jabra Evolve 65", DevicePicture.Headphones("JabraEvolve65.png")),

        // The adapter's name stands in for its headset until Jabra's kit names it, and it comes with the Evolve 65.
        ("Jabra Link 370", DevicePicture.Headphones("JabraEvolve65.png")),
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

    // Capitals and dots don't matter, and runs of spaces count as one.
    private static string Normalize(string name) =>
        string.Join(' ', name.Replace(".", "").ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
}

namespace BluetoothAudioManager.Core.Helpers;

/// <summary>
/// Which picture in the app's Assets folder shows a device, found by its name.
/// To add a device, add its picture to Assets and a line here.
/// </summary>
public static class DevicePictures
{
    // Checked in order, so more specific names come first.
    private static readonly (string NamePart, string FileName)[] ByName =
    [
        ("AirPods Max", "AirPodsMax.png"),
        ("MAJOR IV", "MajorIV.png"),
        ("Jabra Evolve 65", "JabraEvolve65.png"),

        // The adapter's name stands in for its headset until Jabra's kit names it, and it comes with the Evolve 65.
        ("Jabra Link 370", "JabraEvolve65.png"),
    ];

    /// <returns>The picture's file name, or null if there's none for this device and a placeholder should be shown.</returns>
    public static string? FileFor(string deviceName) =>
        ByName.FirstOrDefault(entry => deviceName.Contains(entry.NamePart, StringComparison.OrdinalIgnoreCase)).FileName;
}

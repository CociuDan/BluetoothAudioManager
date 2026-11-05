using System.IO;
using System.Text.Json;

namespace BluetoothAudioManager.App.Models;

/// <summary>The user's preferences, saved in their app data folder. Device readings are never saved here.</summary>
public sealed class AppSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BluetoothAudioManager",
        "settings.json");

    /// <summary>The height the user dragged the window to, or null if they haven't.</summary>
    public double? WindowHeight { get; set; }

    public bool EarDetectionEnabled { get; set; }

    public bool LowBatteryEnabled { get; set; }

    public int LowBatteryLevel { get; set; } = 20;

    /// <summary>Low battery settings for other headphones, by their Windows name.</summary>
    public Dictionary<string, DeviceLowBattery> DeviceLowBattery { get; set; } = [];

    /// <summary>
    /// The partner key Jabra gives registered developers, which its kit needs to read Jabra headsets' battery levels.
    /// It's added to the settings file by hand, so it never ends up in the code.
    /// </summary>
    public string? JabraPartnerKey { get; set; }

    public static AppSettings Load()
    {
        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch (Exception)
        {
            // Missing or unreadable settings just mean the defaults.
            return new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
        }
        catch (Exception)
        {
            // The toggle still works for this run if the file can't be written.
        }
    }
}

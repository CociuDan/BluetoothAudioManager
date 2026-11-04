using System.IO;
using System.Text.Json;

namespace BluetoothAudioManager.App;

/// <summary>The user's preferences, saved in their app data folder. Device readings are never saved here.</summary>
internal sealed class AppSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BluetoothAudioManager",
        "settings.json");

    public bool EarDetectionEnabled { get; set; }

    public bool LowBatteryEnabled { get; set; }

    public int LowBatteryLevel { get; set; } = 20;

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

using System.Text.Json;
using BluetoothAudioManager.Shared.Helpers;

namespace BluetoothAudioManager.Updater.Models;

/// <summary>
/// The updater's settings, in C:\ProgramData\BluetoothAudioManager\updater.json. Read before every wait,
/// so a change takes effect after the next check without restarting anything.
/// </summary>
public sealed class UpdaterSettings
{
    public const int DefaultCheckEveryMinutes = 240;

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>
    /// Whether the updater checks GitHub by itself, on its schedule. Off, it only checks when the app asks,
    /// through Check for updates. Set from the app's settings, for everyone on the PC.
    /// </summary>
    public bool CheckAutomatically { get; set; } = true;

    /// <summary>How often to check GitHub for a new version, in minutes. 1 at the least.</summary>
    public int CheckEveryMinutes { get; set; } = DefaultCheckEveryMinutes;

    public TimeSpan CheckEvery => TimeSpan.FromMinutes(Math.Max(1, CheckEveryMinutes));

    /// <summary>Reads the settings, writing the file with the defaults if it's missing.</summary>
    public static UpdaterSettings Load()
    {
        try
        {
            if (File.Exists(InstallLayout.SettingsPath))
            {
                return JsonSerializer.Deserialize<UpdaterSettings>(File.ReadAllText(InstallLayout.SettingsPath)) ?? new UpdaterSettings();
            }

            var defaults = new UpdaterSettings();
            defaults.Save();
            return defaults;
        }
        catch (Exception)
        {
            // A file that can't be read just means the defaults.
            return new UpdaterSettings();
        }
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(InstallLayout.SettingsPath, JsonSerializer.Serialize(this, WriteOptions));
        }
        catch (Exception exception)
        {
            UpdaterLog.Write($"Couldn't save the updater's settings: {exception.Message}");
        }
    }
}

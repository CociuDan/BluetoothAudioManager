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
            File.WriteAllText(InstallLayout.SettingsPath, JsonSerializer.Serialize(defaults, WriteOptions));
            return defaults;
        }
        catch (Exception)
        {
            // A file that can't be read or written just means the defaults.
            return new UpdaterSettings();
        }
    }
}

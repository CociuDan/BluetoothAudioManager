using System.Text.Json;
using BluetoothAudioManager.Shared.Helpers;

namespace BluetoothAudioManager.Updater.Models;

/// <summary>
/// The updater's settings, in C:\ProgramData\BluetoothAudioManager\updater.json. Read before every wait,
/// so a change takes effect after the next check without restarting anything.
/// </summary>
public sealed class UpdaterSettings
{
    public static readonly TimeSpan DefaultCheckEvery = TimeSpan.FromHours(4);

    // Keeps a mistyped value, such as "00:00:00", from asking GitHub without a pause.
    private static readonly TimeSpan LeastCheckEvery = TimeSpan.FromSeconds(10);

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private TimeSpan _checkEvery = DefaultCheckEvery;

    /// <summary>
    /// Whether the updater checks GitHub by itself, on its schedule. Off, it only checks when the app asks,
    /// through Check for updates. Set from the app's settings, for everyone on the PC.
    /// </summary>
    public bool CheckAutomatically { get; set; } = true;

    /// <summary>
    /// How often to check GitHub for a new version, written in the file as hours:minutes:seconds,
    /// such as "04:00:00", and 10 seconds at the least.
    /// </summary>
    public TimeSpan CheckEvery
    {
        get => _checkEvery;
        set => _checkEvery = value < LeastCheckEvery ? LeastCheckEvery : value;
    }

    /// <summary>
    /// Reads the settings, writing the file with the defaults if it's missing. A file with settings that are
    /// no longer used, or without new ones, is written again with just the current settings.
    /// </summary>
    public static UpdaterSettings Load()
    {
        try
        {
            if (File.Exists(InstallLayout.SettingsPath))
            {
                var text = File.ReadAllText(InstallLayout.SettingsPath);
                var settings = JsonSerializer.Deserialize<UpdaterSettings>(text) ?? new UpdaterSettings();
                if (JsonSerializer.Serialize(settings, WriteOptions) != text)
                {
                    settings.Save();
                }

                return settings;
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

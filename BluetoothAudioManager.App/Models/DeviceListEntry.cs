namespace BluetoothAudioManager.App.Models;

/// <summary>A device in the list being edited: its order key, the name shown, and whether the app shows it.</summary>
public sealed class DeviceListEntry(string key, string name, bool shown)
{
    /// <summary>"AirPods" for the AirPods row, and the Windows name for others, as in the saved order.</summary>
    public string Key { get; } = key;

    public string Name { get; } = name;

    public bool Shown { get; set; } = shown;
}

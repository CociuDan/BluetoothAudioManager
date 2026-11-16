namespace BluetoothAudioManager.App.Models;

/// <summary>
/// A device's volume as last seen or chosen, for while it's disconnected and Windows has no volume for it.
/// </summary>
public sealed class SavedVolume
{
    /// <summary>From 0 to 100%.</summary>
    public int Percent { get; set; }

    public bool IsMuted { get; set; }

    /// <summary>Whether it was changed while the device was disconnected, so it's set the moment the device connects.</summary>
    public bool ApplyOnConnect { get; set; }
}

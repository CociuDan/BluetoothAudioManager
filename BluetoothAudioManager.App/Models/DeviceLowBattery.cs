namespace BluetoothAudioManager.App.Models;

public sealed class DeviceLowBattery
{
    public bool Enabled { get; set; }

    public int Level { get; set; } = 20;
}

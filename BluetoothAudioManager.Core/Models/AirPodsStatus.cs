namespace BluetoothAudioManager.Core.Models;

public sealed record AirPodsStatus(
    string Model,
    BatteryReading Left,
    BatteryReading Right,
    BatteryReading Case,
    bool LeftInEar = false,
    bool RightInEar = false);

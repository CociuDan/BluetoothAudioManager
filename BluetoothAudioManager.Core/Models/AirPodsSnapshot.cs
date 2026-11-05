namespace BluetoothAudioManager.Core.Models;

public sealed record AirPodsSnapshot(
    string Model,
    TrackedBattery Left,
    TrackedBattery Right,
    TrackedBattery Case);

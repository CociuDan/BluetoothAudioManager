namespace BluetoothAudioManager.Core;

public sealed record AirPodsSnapshot(
    string Model,
    TrackedBattery Left,
    TrackedBattery Right,
    TrackedBattery Case);

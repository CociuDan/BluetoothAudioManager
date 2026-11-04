namespace BluetoothAudioManager.Core;

internal sealed record Sample(short Rssi, AirPodsStatus Status, DateTimeOffset ReceivedAt);

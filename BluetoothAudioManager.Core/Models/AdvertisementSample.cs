namespace BluetoothAudioManager.Core.Models;

internal sealed record AdvertisementSample(short Rssi, AirPodsStatus Status, DateTimeOffset ReceivedAt);

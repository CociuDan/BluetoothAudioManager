namespace BluetoothAudioManager.Core;

/// <summary>A battery reading. Percent is null when the part isn't reporting, such as a pod in a closed case.</summary>
public sealed record BatteryReading(int? Percent, bool IsCharging);

namespace BluetoothAudioManager.Core.Models;

/// <summary>
/// A Jabra headset as Jabra's kit reports it. AdapterName is the USB adapter it connects through, if any,
/// and BatteryPercent is null until the headset reports a level.
/// </summary>
public sealed record JabraHeadsetStatus(string Name, string? AdapterName, int? BatteryPercent, bool IsCharging = false);

namespace BluetoothAudioManager.Core.Models;

/// <summary>
/// An audio device paired with this PC, or a headset paired with its own USB adapter, in which case Name is the adapter's.
/// AppleProductId is null for other brands, or if Windows didn't report one.
/// </summary>
public sealed record PairedAudioDevice(string Name, ushort? AppleProductId, bool IsUsbAdapter = false);

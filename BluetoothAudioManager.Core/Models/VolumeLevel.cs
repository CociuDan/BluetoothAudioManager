namespace BluetoothAudioManager.Core.Models;

/// <summary>A sound output's volume, from 0 to 100%, and whether it's muted. Muting keeps the percentage.</summary>
public sealed record VolumeLevel(int Percent, bool IsMuted);

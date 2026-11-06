namespace BluetoothAudioManager.Updater.Models;

/// <summary>A release's installer: its GitHub API address and the SHA-256 checksum GitHub lists for it.</summary>
/// <param name="Version">Such as 0.0.3.</param>
/// <param name="Sha256">Lower-case hex.</param>
public sealed record ReleaseSetup(string Version, string Url, string Sha256);

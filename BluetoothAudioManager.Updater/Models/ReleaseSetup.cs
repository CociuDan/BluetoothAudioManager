namespace BluetoothAudioManager.Updater.Models;

/// <summary>A GitHub release's installer details and release notes.</summary>
/// <param name="Version">Such as 0.0.3.</param>
/// <param name="Sha256">Lower-case hex.</param>
public sealed record ReleaseSetup(string Version, string Url, string Sha256, string Notes);

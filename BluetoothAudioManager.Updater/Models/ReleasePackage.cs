namespace BluetoothAudioManager.Updater.Models;

/// <summary>A release's package: the app and updater zipped, and its SHA-256 checksum, as GitHub API addresses.</summary>
/// <param name="Version">Such as 0.0.3.</param>
public sealed record ReleasePackage(string Version, string PackageUrl, string ChecksumUrl);

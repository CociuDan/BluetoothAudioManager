using BluetoothAudioManager.Shared.Enums;

namespace BluetoothAudioManager.Shared.Models;

/// <param name="Version">The version ready to install, when the state is Ready.</param>
/// <param name="Notes">The release notes for the ready version, when available.</param>
public sealed record UpdateStatus(UpdateState State, string? Version = null, string? Notes = null);

namespace BluetoothAudioManager.Updater.Models;

/// <summary>The program asking the updater something, as Windows reports it, not as the program says.</summary>
/// <param name="SessionId">The Windows session it runs in, where the app is opened again after an update.</param>
/// <param name="ExecutablePath">Its program file, or null if it couldn't be read.</param>
public sealed record PipeClient(uint SessionId, string? ExecutablePath);

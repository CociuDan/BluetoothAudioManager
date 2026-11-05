using BluetoothAudioManager.Core.Enums;

namespace BluetoothAudioManager.Core.Interfaces;

/// <summary>Follows an audio device's connection, and makes it the system output. Events are raised on a background thread.</summary>
public interface IAudioDeviceConnection : IDisposable
{
    event Action<DeviceConnectionState>? StateChanged;

    DeviceConnectionState State { get; }

    /// <summary>Whether the app can connect and disconnect the device, which it can't for headsets on their own USB adapter.</summary>
    bool CanConnect { get; }

    /// <summary>Checks for the device again, so one added or removed while the app runs is noticed.</summary>
    Task FindDevicesAsync();

    Task<DeviceActionResult> ConnectAsync(TimeSpan timeout);

    Task<DeviceActionResult> DisconnectAsync(TimeSpan timeout);

    Task<DeviceActionResult> ReconnectAsync(TimeSpan timeout);

    Task<bool> IsDefaultOutputAsync();

    /// <returns>Whether Windows accepted the change.</returns>
    Task<bool> SetAsDefaultOutputAsync();
}

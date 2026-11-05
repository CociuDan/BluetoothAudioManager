using BluetoothAudioManager.Core.Enums;
using BluetoothAudioManager.Core.Interfaces;

namespace BluetoothAudioManager.Core.Services;

/// <summary>
/// Follows a headset on its own USB adapter, by whether the adapter's sound output is there.
/// The adapter stays when the headset is off, so this can't tell whether the headset itself is on.
/// </summary>
/// <param name="adapterName">The adapter's name, such as "Jabra Link 370".</param>
public sealed class UsbHeadsetMonitor(string adapterName) : IAudioDeviceConnection
{
    public event Action<DeviceConnectionState>? StateChanged;

    public DeviceConnectionState State { get; private set; } = DeviceConnectionState.NotPaired;

    public bool CanConnect => false;

    public bool Matches(string name) => name.Contains(adapterName, StringComparison.OrdinalIgnoreCase);

    public async Task FindDevicesAsync()
    {
        var present = await Task.Run(() => AudioOutputs.FindActiveNames().Any(Matches));
        State = present ? DeviceConnectionState.Connected : DeviceConnectionState.NotPaired;
        StateChanged?.Invoke(State);
    }

    // The adapter keeps its own connection to the headset, so the app has nothing to ask Windows for.
    public Task<DeviceActionResult> ConnectAsync(TimeSpan timeout) => Task.FromResult(DeviceActionResult.NoAudioOutput);

    public Task<DeviceActionResult> DisconnectAsync(TimeSpan timeout) => Task.FromResult(DeviceActionResult.NoAudioOutput);

    public Task<DeviceActionResult> ReconnectAsync(TimeSpan timeout) => Task.FromResult(DeviceActionResult.NoAudioOutput);

    public Task<bool> IsDefaultOutputAsync() =>
        Task.Run(() => BluetoothAudioEndpoint.IsDefaultOutput(Matches));

    public Task<bool> SetAsDefaultOutputAsync() =>
        Task.Run(() => BluetoothAudioEndpoint.SetDefaultOutput(Matches));

    public void Dispose()
    {
        // Nothing is held open between checks.
    }
}

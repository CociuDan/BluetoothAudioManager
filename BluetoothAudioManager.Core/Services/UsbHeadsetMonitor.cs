using BluetoothAudioManager.Core.Enums;
using BluetoothAudioManager.Core.Interfaces;

namespace BluetoothAudioManager.Core.Services;

/// <summary>
/// Follows a headset on its own USB adapter. With Jabra's kit running, the headset counts as connected
/// while the kit sees it through the adapter. Without it, it counts as connected whenever the adapter is plugged in,
/// because the adapter's sound output is all the app can see.
/// </summary>
/// <param name="adapterName">The adapter's name, such as "Jabra Link 370".</param>
/// <param name="jabra">Jabra's kit, for the headset's real connection and for connecting it.</param>
public sealed class UsbHeadsetMonitor(string adapterName, JabraHeadsets? jabra = null) : IAudioDeviceConnection
{
    private static readonly TimeSpan ReconnectPause = TimeSpan.FromSeconds(1);

    public event Action<DeviceConnectionState>? StateChanged;

    public DeviceConnectionState State { get; private set; } = DeviceConnectionState.NotPaired;

    public bool CanConnect => jabra?.CanConnect(adapterName) == true;

    public bool Matches(string name) => name.Contains(adapterName, StringComparison.OrdinalIgnoreCase);

    public async Task FindDevicesAsync()
    {
        var present = await Task.Run(() => AudioOutputs.FindActiveNames().Any(Matches));

        State = !present ? DeviceConnectionState.NotPaired
            : jabra is not { IsRunning: true } ? DeviceConnectionState.Connected
            : jabra.FindByAdapter(adapterName) is not null ? DeviceConnectionState.Connected
            : DeviceConnectionState.Disconnected;
        StateChanged?.Invoke(State);
    }

    /// <summary>Asks the adapter to connect its headset, then waits for the kit to see it.</summary>
    public async Task<DeviceActionResult> ConnectAsync(TimeSpan timeout)
    {
        try
        {
            if (jabra is null || !await jabra.ConnectAsync(adapterName, timeout))
            {
                return DeviceActionResult.TimedOut;
            }
        }
        catch (Exception)
        {
            return DeviceActionResult.TimedOut;
        }

        return await WaitForStateAsync(DeviceConnectionState.Connected, timeout);
    }

    /// <summary>Asks the adapter to disconnect its headset, then waits for the kit to stop seeing it.</summary>
    public async Task<DeviceActionResult> DisconnectAsync(TimeSpan timeout)
    {
        try
        {
            if (jabra is null || !await jabra.DisconnectAsync(adapterName))
            {
                return DeviceActionResult.TimedOut;
            }
        }
        catch (Exception)
        {
            return DeviceActionResult.TimedOut;
        }

        return await WaitForStateAsync(DeviceConnectionState.Disconnected, timeout);
    }

    public async Task<DeviceActionResult> ReconnectAsync(TimeSpan timeout)
    {
        var disconnected = await DisconnectAsync(timeout);
        if (disconnected != DeviceActionResult.Succeeded)
        {
            return disconnected;
        }

        await Task.Delay(ReconnectPause);
        return await ConnectAsync(timeout);
    }

    public Task<bool> IsDefaultOutputAsync() =>
        Task.Run(() => BluetoothAudioEndpoint.IsDefaultOutput(Matches));

    public Task<bool> SetAsDefaultOutputAsync() =>
        Task.Run(() => BluetoothAudioEndpoint.SetDefaultOutput(Matches));

    private async Task<DeviceActionResult> WaitForStateAsync(DeviceConnectionState state, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.Now + timeout;
        while (DateTimeOffset.Now < deadline)
        {
            await FindDevicesAsync();
            if (State == state)
            {
                return DeviceActionResult.Succeeded;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        return DeviceActionResult.TimedOut;
    }

    public void Dispose()
    {
        // Nothing is held open between checks.
    }
}

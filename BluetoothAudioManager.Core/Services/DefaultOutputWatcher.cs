using BluetoothAudioManager.Core.Interop;

namespace BluetoothAudioManager.Core.Services;

/// <summary>
/// Reports as soon as Windows' sound output changes, however it was changed.
/// Events are raised on a Windows audio thread, so handlers must return quickly.
/// </summary>
public sealed class DefaultOutputWatcher : IMMNotificationClient, IDisposable
{
    private const int Success = 0;
    private const uint RenderFlow = 0;
    private const uint MultimediaRole = 1;

    private readonly IMMDeviceEnumerator _enumerator;

    public DefaultOutputWatcher()
    {
        _enumerator = AudioDeviceEnumerator.Create();
        _enumerator.RegisterEndpointNotificationCallback(this);
    }

    public event Action? Changed;

    public int OnDeviceStateChanged(string id, uint state)
    {
        Changed?.Invoke();
        return Success;
    }

    public int OnDeviceAdded(string id) => Success;

    public int OnDeviceRemoved(string id) => Success;

    public int OnDefaultDeviceChanged(uint flow, uint role, string? id)
    {
        // Windows sends one of these per role, and each output change only needs one refresh.
        if (flow == RenderFlow && role == MultimediaRole)
        {
            Changed?.Invoke();
        }

        return Success;
    }

    public int OnPropertyValueChanged(string id, AudioPropertyKey key) => Success;

    public void Dispose() => _enumerator.UnregisterEndpointNotificationCallback(this);
}

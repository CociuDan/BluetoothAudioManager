using System.Runtime.InteropServices;

namespace BluetoothAudioManager.Core;

/// <summary>
/// Reports as soon as Windows' sound output changes, however it was changed.
/// Events are raised on a Windows audio thread, so handlers must return quickly.
/// </summary>
public sealed class DefaultOutputWatcher : IMMNotificationClient, IDisposable
{
    private const int Success = 0;
    private const uint RenderFlow = 0;
    private const uint MultimediaRole = 1;

    private readonly IDeviceEnumerator _enumerator;

    public DefaultOutputWatcher()
    {
        // Created by ID, because declaring this class in two places makes the runtime confuse the two.
        var classId = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
        _enumerator = (IDeviceEnumerator)Activator.CreateInstance(Type.GetTypeFromCLSID(classId)!)!;
        _enumerator.RegisterEndpointNotificationCallback(this);
    }

    public event Action? Changed;

    public void Dispose() => _enumerator.UnregisterEndpointNotificationCallback(this);

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

    // The unused methods only hold their places, because COM calls methods by position.
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints();
        [PreserveSig] int GetDefaultAudioEndpoint();
        [PreserveSig] int GetDevice();
        void RegisterEndpointNotificationCallback(IMMNotificationClient client);
        void UnregisterEndpointNotificationCallback(IMMNotificationClient client);
    }
}

// Public because Windows can only call back into interfaces visible to COM.
[ComImport, Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMMNotificationClient
{
    [PreserveSig] int OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string id, uint state);
    [PreserveSig] int OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string id);
    [PreserveSig] int OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string id);
    [PreserveSig] int OnDefaultDeviceChanged(uint flow, uint role, [MarshalAs(UnmanagedType.LPWStr)] string? id);
    [PreserveSig] int OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string id, AudioPropertyKey key);
}

[StructLayout(LayoutKind.Sequential)]
public struct AudioPropertyKey
{
    public Guid Format;
    public uint Id;
}

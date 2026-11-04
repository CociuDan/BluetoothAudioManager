using System.Runtime.InteropServices;

namespace BluetoothAudioManager.Core;

/// <summary>
/// A Windows audio output whose driver can be asked to connect or disconnect its Bluetooth device.
/// A successful request only means the driver tried, so callers must check the connection afterwards.
/// </summary>
public sealed class BluetoothAudioEndpoint
{
    private static readonly Guid BluetoothAudioPropertySet = new("7FA06C40-B8F6-4C7E-8556-E8C33A12E54D");

    private const uint OneShotReconnect = 0;
    private const uint OneShotDisconnect = 1;
    private const uint PropertyTypeGet = 1;
    private const uint RenderFlow = 0;
    private const uint AllDeviceStates = 0xF;
    private const uint ClsCtxAll = 0x17;
    private const ushort VtLpwstr = 31;

    private readonly IKsControl _control;

    private BluetoothAudioEndpoint(string name, IKsControl control)
    {
        Name = name;
        _control = control;
    }

    public string Name { get; }

    public bool RequestConnect() => SendRequest(OneShotReconnect);

    public bool RequestDisconnect() => SendRequest(OneShotDisconnect);

    private bool SendRequest(uint request)
    {
        var property = new KsProperty(BluetoothAudioPropertySet, request, PropertyTypeGet);
        return _control.KsProperty(ref property, (uint)Marshal.SizeOf<KsProperty>(), IntPtr.Zero, 0, out _) >= 0;
    }

    /// <summary>Finds matching audio outputs, including unplugged ones, as disconnected Bluetooth devices are.</summary>
    public static IReadOnlyList<BluetoothAudioEndpoint> Find(Func<string, bool> nameMatches)
    {
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
        enumerator.EnumAudioEndpoints(RenderFlow, AllDeviceStates, out var devices);
        devices.GetCount(out var count);

        var endpoints = new List<BluetoothAudioEndpoint>();
        for (uint i = 0; i < count; i++)
        {
            devices.Item(i, out var device);

            try
            {
                var name = GetFriendlyName(device);
                if (name is not null && nameMatches(name))
                {
                    endpoints.Add(new BluetoothAudioEndpoint(name, GetControl(enumerator, device)));
                }
            }
            catch (Exception)
            {
                // Removed devices can't be read and not every driver exposes this control,
                // and COM reports these as several exception types.
            }
        }

        return endpoints;
    }

    private static string? GetFriendlyName(IMMDevice device)
    {
        const uint StgmRead = 0;
        var friendlyName = new PropertyKey(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 14);

        device.OpenPropertyStore(StgmRead, out var store);
        store.GetValue(ref friendlyName, out var value);
        try
        {
            return value.Type == VtLpwstr ? Marshal.PtrToStringUni(value.Pointer) : null;
        }
        finally
        {
            PropVariantClear(ref value);
        }
    }

    private static IKsControl GetControl(IMMDeviceEnumerator enumerator, IMMDevice endpoint)
    {
        var topologyId = typeof(IDeviceTopology).GUID;
        endpoint.Activate(ref topologyId, ClsCtxAll, IntPtr.Zero, out var topology);
        ((IDeviceTopology)topology).GetConnector(0, out var connector);
        connector.GetDeviceIdConnectedTo(out var filterId);
        enumerator.GetDevice(filterId, out var filter);

        var controlId = typeof(IKsControl).GUID;
        filter.Activate(ref controlId, ClsCtxAll, IntPtr.Zero, out var control);
        return (IKsControl)control;
    }

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant value);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct KsProperty(Guid Set, uint Id, uint Flags);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct PropertyKey(Guid Format, uint Id);

    [StructLayout(LayoutKind.Sequential)]
    private struct PropVariant
    {
        public ushort Type;
        public ushort Reserved1;
        public ushort Reserved2;
        public ushort Reserved3;
        public IntPtr Pointer;
        public IntPtr Extra;
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumerator;

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        void EnumAudioEndpoints(uint dataFlow, uint stateMask, out IMMDeviceCollection devices);
        void GetDefaultAudioEndpoint(uint dataFlow, uint role, out IMMDevice device);
        void GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        void GetCount(out uint count);
        void Item(uint index, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        void Activate(ref Guid iid, uint clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
        void OpenPropertyStore(uint access, out IPropertyStore store);
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        void GetCount(out uint count);
        void GetAt(uint index, out PropertyKey key);
        void GetValue(ref PropertyKey key, out PropVariant value);
    }

    [ComImport, Guid("2A07407E-6497-4A18-9787-32F79BD0D98F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDeviceTopology
    {
        void GetConnectorCount(out uint count);
        void GetConnector(uint index, out IConnector connector);
    }

    [ComImport, Guid("9C2C4058-23F5-41DE-877A-DF3AF236A09E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IConnector
    {
        void GetType(out int type);
        void GetDataFlow(out int flow);
        void ConnectTo(IConnector other);
        void Disconnect();
        void IsConnected([MarshalAs(UnmanagedType.Bool)] out bool connected);
        void GetConnectedTo(out IConnector other);
        void GetConnectorIdConnectedTo([MarshalAs(UnmanagedType.LPWStr)] out string id);
        void GetDeviceIdConnectedTo([MarshalAs(UnmanagedType.LPWStr)] out string id);
    }

    [ComImport, Guid("28F54685-06FD-11D2-B27A-00A0C9223196"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IKsControl
    {
        [PreserveSig]
        int KsProperty(ref KsProperty property, uint propertyLength, IntPtr data, uint dataLength, out uint bytesReturned);
    }
}

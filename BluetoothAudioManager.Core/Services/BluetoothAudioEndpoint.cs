using System.Runtime.InteropServices;
using BluetoothAudioManager.Core.Interop;

namespace BluetoothAudioManager.Core.Services;

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
    private const uint ActiveDisabledOrUnplugged = 0xB;
    private const uint ActiveOnly = 0x1;
    private const uint ConsoleRole = 0;
    private const uint MultimediaRole = 1;
    private const uint CommunicationsRole = 2;
    private const uint ClsCtxAll = 0x17;
    private const ushort VtLpwstr = 31;

    private readonly IKsControl _control;

    private BluetoothAudioEndpoint(string name, IKsControl control)
    {
        Name = name;
        _control = control;
    }

    public string Name { get; }

    /// <summary>
    /// Finds matching audio outputs, including unplugged ones, as disconnected Bluetooth devices are.
    /// Outputs marked not present are skipped, because their device has been removed from Windows.
    /// </summary>
    public static IReadOnlyList<BluetoothAudioEndpoint> Find(Func<string, bool> nameMatches)
    {
        var enumerator = AudioDeviceEnumerator.Create();
        enumerator.EnumAudioEndpoints(RenderFlow, ActiveDisabledOrUnplugged, out var devices);
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

    /// <summary>Whether a matching output is what Windows currently plays sound through.</summary>
    public static bool IsDefaultOutput(Func<string, bool> nameMatches)
    {
        var enumerator = AudioDeviceEnumerator.Create();
        var id = FindPreferredOutputId(enumerator, nameMatches);
        if (id is null)
        {
            return false;
        }

        try
        {
            enumerator.GetDefaultAudioEndpoint(RenderFlow, MultimediaRole, out var current);
            current.GetId(out var currentId);
            return currentId == id;
        }
        catch (Exception)
        {
            // Windows reports having no default output as an error.
            return false;
        }
    }

    /// <summary>
    /// Makes a matching output the Windows default. Windows has no public API for this,
    /// so it uses the undocumented interface that audio switchers rely on, which an update could change.
    /// </summary>
    /// <returns>Whether Windows accepted the change.</returns>
    public static bool SetDefaultOutput(Func<string, bool> nameMatches)
    {
        var enumerator = AudioDeviceEnumerator.Create();
        var id = FindPreferredOutputId(enumerator, nameMatches);
        if (id is null)
        {
            return false;
        }

        var policy = (IPolicyConfig)new PolicyConfigClient();
        return policy.SetDefaultEndpoint(id, ConsoleRole) >= 0
            && policy.SetDefaultEndpoint(id, MultimediaRole) >= 0
            && policy.SetDefaultEndpoint(id, CommunicationsRole) >= 0;
    }

    public bool RequestConnect() => SendRequest(OneShotReconnect);

    public bool RequestDisconnect() => SendRequest(OneShotDisconnect);

    // A Bluetooth headset has a stereo output and a much lower quality Hands-Free one, so stereo comes first.
    private static string? FindPreferredOutputId(IMMDeviceEnumerator enumerator, Func<string, bool> nameMatches)
    {
        enumerator.EnumAudioEndpoints(RenderFlow, ActiveOnly, out var devices);
        devices.GetCount(out var count);

        string? fallback = null;
        for (uint i = 0; i < count; i++)
        {
            devices.Item(i, out var device);

            try
            {
                var name = GetFriendlyName(device);
                if (name is null || !nameMatches(name))
                {
                    continue;
                }

                device.GetId(out var id);
                if (!name.Contains("Hands-Free", StringComparison.OrdinalIgnoreCase))
                {
                    return id;
                }

                fallback ??= id;
            }
            catch (Exception)
            {
                // An output that can't be read is skipped, as in Find.
            }
        }

        return fallback;
    }

    internal static string? GetFriendlyName(IMMDevice device)
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

    private bool SendRequest(uint request)
    {
        var property = new KsProperty(BluetoothAudioPropertySet, request, PropertyTypeGet);
        return _control.KsProperty(ref property, (uint)Marshal.SizeOf<KsProperty>(), IntPtr.Zero, 0, out _) >= 0;
    }
}

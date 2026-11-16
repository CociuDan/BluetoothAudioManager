using System.Runtime.InteropServices;
using BluetoothAudioManager.Core.Interop;
using BluetoothAudioManager.Core.Models;

namespace BluetoothAudioManager.Core.Services;

/// <summary>
/// Follows and changes Windows' volume for one device's sound output, the stereo one if it has two. Windows keeps a
/// volume for each output and uses it whenever that output plays, so this works whether or not it's the system output.
/// A device that isn't connected has no sound output, and so no volume.
/// </summary>
/// <param name="nameMatches">Decides which Windows names of sound outputs belong to the device.</param>
public sealed class DeviceVolume(Func<string, bool> nameMatches) : IAudioEndpointVolumeCallback, IDisposable
{
    private const int Success = 0;
    private const uint ClsCtxAll = 0x17;

    // Marks the app's own changes, so Windows' report of them doesn't move a slider while it's being dragged.
    private static readonly Guid OwnChange = Guid.NewGuid();

    private readonly object _gate = new();
    private IAudioEndpointVolume? _endpoint;
    private string? _endpointId;

    /// <summary>
    /// Raised with the new volume when it's changed anywhere else, such as with the keyboard's volume keys or Windows'
    /// taskbar. Raised on a Windows audio thread, so handlers must return quickly.
    /// </summary>
    public event Action<VolumeLevel>? Changed;

    /// <summary>The volume, or null while the device has no sound output, such as while it's disconnected.</summary>
    public VolumeLevel? Current { get; private set; }

    /// <summary>Finds the device's sound output again, as connecting and disconnecting add and remove it, and reads its volume.</summary>
    public VolumeLevel? Refresh()
    {
        string? id = null;
        IAudioEndpointVolume? found = null;

        try
        {
            var enumerator = AudioDeviceEnumerator.Create();
            id = BluetoothAudioEndpoint.FindPreferredOutputId(enumerator, nameMatches);
            if (id is not null && id != _endpointId)
            {
                enumerator.GetDevice(id, out var device);
                var interfaceId = typeof(IAudioEndpointVolume).GUID;
                device.Activate(ref interfaceId, ClsCtxAll, IntPtr.Zero, out var instance);
                found = (IAudioEndpointVolume)instance;
            }
        }
        catch (Exception)
        {
            // An output that can't be read counts as none, as elsewhere.
            id = null;
        }

        lock (_gate)
        {
            if (id != _endpointId)
            {
                Release();
                if (found is not null)
                {
                    found.RegisterControlChangeNotify(this);
                    _endpoint = found;
                    _endpointId = id;
                }
            }

            Current = Read();
            return Current;
        }
    }

    /// <summary>Sets the volume, from 0 to 100%. Muting stays as it is.</summary>
    public void SetPercent(int percent)
    {
        Change(endpoint =>
        {
            var context = OwnChange;
            endpoint.SetMasterVolumeLevelScalar(Math.Clamp(percent, 0, 100) / 100f, ref context);
        });
    }

    public void SetMuted(bool muted)
    {
        Change(endpoint =>
        {
            var context = OwnChange;
            endpoint.SetMute(muted, ref context);
        });
    }

    public int OnNotify(IntPtr notificationData)
    {
        try
        {
            var data = Marshal.PtrToStructure<AudioVolumeNotificationData>(notificationData);
            var level = new VolumeLevel(ToPercent(data.MasterVolume), data.Muted != 0);
            Current = level;
            if (data.EventContext != OwnChange)
            {
                Changed?.Invoke(level);
            }
        }
        catch (Exception)
        {
            // A report that can't be read is skipped. The next one, or the next refresh, catches up.
        }

        return Success;
    }

    private static int ToPercent(float level) => (int)Math.Round(Math.Clamp(level, 0f, 1f) * 100);

    private void Change(Action<IAudioEndpointVolume> change)
    {
        lock (_gate)
        {
            if (_endpoint is null)
            {
                return;
            }

            try
            {
                change(_endpoint);
                Current = Read();
            }
            catch (Exception)
            {
                // The output went away meanwhile. The next refresh notices.
            }
        }
    }

    private VolumeLevel? Read()
    {
        if (_endpoint is null)
        {
            return null;
        }

        try
        {
            _endpoint.GetMasterVolumeLevelScalar(out var level);
            _endpoint.GetMute(out var muted);
            return new VolumeLevel(ToPercent(level), muted);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void Release()
    {
        if (_endpoint is not null)
        {
            try
            {
                _endpoint.UnregisterControlChangeNotify(this);
            }
            catch (Exception)
            {
                // The output is already gone.
            }
        }

        _endpoint = null;
        _endpointId = null;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            Release();
            Current = null;
        }
    }
}

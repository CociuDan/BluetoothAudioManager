using System.Runtime.InteropServices;

namespace BluetoothAudioManager.Core.Interop;

// The start of what Windows sends with a volume change. The volume of each channel follows, which the app doesn't read.
[StructLayout(LayoutKind.Sequential)]
internal struct AudioVolumeNotificationData
{
    public Guid EventContext;
    public int Muted;
    public float MasterVolume;
    public uint ChannelCount;
}

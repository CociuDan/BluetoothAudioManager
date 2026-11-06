using System.Runtime.InteropServices;

namespace BluetoothAudioManager.Installer.Interop;

/// <summary>Windows' LUID, which names a privilege.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct Luid
{
    public uint LowPart;
    public int HighPart;
}

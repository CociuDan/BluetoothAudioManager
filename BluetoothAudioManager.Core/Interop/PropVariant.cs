using System.Runtime.InteropServices;

namespace BluetoothAudioManager.Core.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct PropVariant
{
    public ushort Type;
    public ushort Reserved1;
    public ushort Reserved2;
    public ushort Reserved3;
    public IntPtr Pointer;
    public IntPtr Extra;
}

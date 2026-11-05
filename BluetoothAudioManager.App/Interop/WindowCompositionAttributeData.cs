using System.Runtime.InteropServices;

namespace BluetoothAudioManager.App.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct WindowCompositionAttributeData
{
    public int Attribute;
    public IntPtr Data;
    public int SizeOfData;
}

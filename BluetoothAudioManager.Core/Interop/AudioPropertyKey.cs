using System.Runtime.InteropServices;

namespace BluetoothAudioManager.Core.Interop;

[StructLayout(LayoutKind.Sequential)]
public struct AudioPropertyKey
{
    public Guid Format;
    public uint Id;
}

using System.Runtime.InteropServices;

namespace BluetoothAudioManager.Core.Interop;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct KsProperty(Guid Set, uint Id, uint Flags);

using System.Runtime.InteropServices;

namespace BluetoothAudioManager.Updater.Interop;

/// <summary>Windows' PROCESS_INFORMATION, filled in when the app is started in a user's session.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ProcessInformation
{
    public IntPtr Process;
    public IntPtr Thread;
    public int ProcessId;
    public int ThreadId;
}

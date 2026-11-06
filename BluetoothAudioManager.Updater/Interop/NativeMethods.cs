using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace BluetoothAudioManager.Updater.Interop;

/// <summary>The Windows functions the updater needs to tell which program, in which session, is asking it something.</summary>
internal static class NativeMethods
{
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ProcessIdToSessionId(uint processId, out uint sessionId);
}

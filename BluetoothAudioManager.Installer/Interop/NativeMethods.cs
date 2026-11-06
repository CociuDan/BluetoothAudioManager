using System.Runtime.InteropServices;

namespace BluetoothAudioManager.Installer.Interop;

/// <summary>
/// The Windows functions the uninstaller needs: opening the registry of people who aren't signed in,
/// and deleting its own temporary copy once Windows restarts.
/// </summary>
internal static class NativeMethods
{
    public const uint TokenAdjustPrivileges = 0x0020;
    public const uint TokenQuery = 0x0008;
    public const uint MoveFileDelayUntilReboot = 0x4;

    /// <summary>HKEY_USERS, where a user's registry file is opened.</summary>
    public static readonly IntPtr UsersHive = new(unchecked((int)0x80000003));

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    public static extern int RegLoadKey(IntPtr key, string subKey, string file);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    public static extern int RegUnLoadKey(IntPtr key, string subKey);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool OpenProcessToken(IntPtr process, uint desiredAccess, out IntPtr token);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool LookupPrivilegeValue(string? systemName, string name, out Luid luid);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AdjustTokenPrivileges(
        IntPtr token, [MarshalAs(UnmanagedType.Bool)] bool disableAll, ref TokenPrivilege newState, uint bufferLength, IntPtr previousState, IntPtr returnLength);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool MoveFileEx(string existing, string? replacement, uint flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(IntPtr handle);
}

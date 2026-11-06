using System.Runtime.InteropServices;

namespace BluetoothAudioManager.Installer.Interop;

/// <summary>Windows' TOKEN_PRIVILEGES with a single privilege, for turning one on.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct TokenPrivilege
{
    public const uint Enabled = 0x00000002;

    public uint PrivilegeCount;
    public Luid Luid;
    public uint Attributes;
}

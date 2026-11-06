using System.ComponentModel;
using System.Diagnostics;
using BluetoothAudioManager.Installer.Interop;
using Microsoft.Win32;

namespace BluetoothAudioManager.Installer.Services;

/// <summary>
/// A registry file of someone who isn't signed in, such as their NTUSER.DAT, opened under HKEY_USERS until disposed.
/// Windows only allows this with the backup and restore privileges, which administrators have but must turn on.
/// </summary>
internal sealed class UserHive : IDisposable
{
    private static bool _privilegesOn;

    private readonly string _name;

    public RegistryKey Root { get; }

    /// <summary>Opens the file under a temporary name, or returns null if it can't be opened, such as when it's in use.</summary>
    public static UserHive? Open(string file, string name)
    {
        TurnOnPrivileges();

        if (NativeMethods.RegLoadKey(NativeMethods.UsersHive, name, file) != 0)
        {
            return null;
        }

        using var users = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Registry64);
        var root = users.OpenSubKey(name, writable: true);
        if (root is null)
        {
            NativeMethods.RegUnLoadKey(NativeMethods.UsersHive, name);
            return null;
        }

        return new UserHive(name, root);
    }

    private UserHive(string name, RegistryKey root)
    {
        _name = name;
        Root = root;
    }

    private static void TurnOnPrivileges()
    {
        if (_privilegesOn)
        {
            return;
        }

        if (!NativeMethods.OpenProcessToken(Process.GetCurrentProcess().Handle,
                NativeMethods.TokenAdjustPrivileges | NativeMethods.TokenQuery, out var token))
        {
            throw new Win32Exception();
        }

        try
        {
            foreach (var privilege in new[] { "SeBackupPrivilege", "SeRestorePrivilege" })
            {
                if (!NativeMethods.LookupPrivilegeValue(null, privilege, out var luid))
                {
                    throw new Win32Exception();
                }

                var state = new TokenPrivilege { PrivilegeCount = 1, Luid = luid, Attributes = TokenPrivilege.Enabled };
                NativeMethods.AdjustTokenPrivileges(token, false, ref state, 0, IntPtr.Zero, IntPtr.Zero);
            }

            _privilegesOn = true;
        }
        finally
        {
            NativeMethods.CloseHandle(token);
        }
    }

    public void Dispose()
    {
        Root.Dispose();

        // Windows only closes the file once nothing holds a key in it.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        NativeMethods.RegUnLoadKey(NativeMethods.UsersHive, _name);
    }
}

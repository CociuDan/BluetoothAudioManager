using System.ComponentModel;
using System.Runtime.InteropServices;
using BluetoothAudioManager.Shared.Interop;

namespace BluetoothAudioManager.Shared.Helpers;

/// <summary>
/// Starts a program as the user signed in to a Windows session, with that user's own rights, not those of the program starting it.
/// Updates run as Windows itself, so this is how the app is opened again for the person who asked for the update.
/// </summary>
public static class UserSessionLauncher
{
    /// <summary>Starts the program on the session's desktop. Throws if the session has nobody signed in.</summary>
    public static void Start(uint sessionId, string path, string arguments)
    {
        var userToken = IntPtr.Zero;
        var primaryToken = IntPtr.Zero;
        var environment = IntPtr.Zero;

        try
        {
            if (!SessionNativeMethods.WTSQueryUserToken(sessionId, out userToken)
                || !SessionNativeMethods.DuplicateTokenEx(
                    userToken, SessionNativeMethods.MaximumAllowed, IntPtr.Zero,
                    SessionNativeMethods.SecurityImpersonation, SessionNativeMethods.TokenPrimary, out primaryToken)
                || !SessionNativeMethods.CreateEnvironmentBlock(out environment, primaryToken, false))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var startup = new StartupInfo { Size = Marshal.SizeOf<StartupInfo>(), Desktop = @"winsta0\default" };
            var commandLine = $"\"{path}\" {arguments}".TrimEnd();

            if (!SessionNativeMethods.CreateProcessAsUser(
                    primaryToken, path, commandLine, IntPtr.Zero, IntPtr.Zero, false,
                    SessionNativeMethods.CreateUnicodeEnvironment, environment, Path.GetDirectoryName(path),
                    ref startup, out var process))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            SessionNativeMethods.CloseHandle(process.Thread);
            SessionNativeMethods.CloseHandle(process.Process);
        }
        finally
        {
            if (environment != IntPtr.Zero)
            {
                SessionNativeMethods.DestroyEnvironmentBlock(environment);
            }

            if (primaryToken != IntPtr.Zero)
            {
                SessionNativeMethods.CloseHandle(primaryToken);
            }

            if (userToken != IntPtr.Zero)
            {
                SessionNativeMethods.CloseHandle(userToken);
            }
        }
    }
}

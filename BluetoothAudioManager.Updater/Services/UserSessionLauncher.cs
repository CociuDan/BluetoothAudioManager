using System.ComponentModel;
using System.Runtime.InteropServices;
using BluetoothAudioManager.Updater.Interop;

namespace BluetoothAudioManager.Updater.Services;

/// <summary>
/// Starts a program as the user signed in to a Windows session, with that user's own rights, not the updater's.
/// The updater runs as Windows itself, so this is how it opens the app again for the person who asked for the update.
/// </summary>
internal static class UserSessionLauncher
{
    /// <summary>Starts the program on the session's desktop. Throws if the session has nobody signed in.</summary>
    public static void Start(uint sessionId, string path, string arguments)
    {
        var userToken = IntPtr.Zero;
        var primaryToken = IntPtr.Zero;
        var environment = IntPtr.Zero;

        try
        {
            if (!NativeMethods.WTSQueryUserToken(sessionId, out userToken)
                || !NativeMethods.DuplicateTokenEx(
                    userToken, NativeMethods.MaximumAllowed, IntPtr.Zero,
                    NativeMethods.SecurityImpersonation, NativeMethods.TokenPrimary, out primaryToken)
                || !NativeMethods.CreateEnvironmentBlock(out environment, primaryToken, false))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var startup = new StartupInfo { Size = Marshal.SizeOf<StartupInfo>(), Desktop = @"winsta0\default" };
            var commandLine = $"\"{path}\" {arguments}".TrimEnd();

            if (!NativeMethods.CreateProcessAsUser(
                    primaryToken, path, commandLine, IntPtr.Zero, IntPtr.Zero, false,
                    NativeMethods.CreateUnicodeEnvironment, environment, Path.GetDirectoryName(path),
                    ref startup, out var process))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            NativeMethods.CloseHandle(process.Thread);
            NativeMethods.CloseHandle(process.Process);
        }
        finally
        {
            if (environment != IntPtr.Zero)
            {
                NativeMethods.DestroyEnvironmentBlock(environment);
            }

            if (primaryToken != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(primaryToken);
            }

            if (userToken != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(userToken);
            }
        }
    }
}

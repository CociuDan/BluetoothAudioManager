using System.Diagnostics;
using BluetoothAudioManager.Shared.Helpers;

namespace BluetoothAudioManager.Installer.Services;

/// <summary>The app as it runs for anyone signed in, which must close before its files can be replaced or removed.</summary>
internal static class RunningApp
{
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Closes every copy of the app. It only follows devices, so closing it loses nothing.</summary>
    public static void CloseEverywhere()
    {
        foreach (var process in Process.GetProcessesByName(InstallLayout.AppProcessName))
        {
            using (process)
            {
                try
                {
                    process.Kill();
                    process.WaitForExit(ExitTimeout);
                }
                catch (Exception)
                {
                    // Already closed.
                }
            }
        }
    }
}

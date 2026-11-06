using System.Diagnostics;
using System.IO;
using BluetoothAudioManager.Shared.Helpers;

namespace BluetoothAudioManager.Installer.Services;

/// <summary>The app as it runs for anyone signed in, which must close before its files can be replaced or removed.</summary>
internal static class RunningApp
{
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Closes every copy of the app, and any other program running from the install folder, such as a helper a
    /// device kit started, as they keep its files locked. The app only follows devices, so closing it loses nothing.
    /// </summary>
    public static void CloseEverywhere(string? installFolder)
    {
        var folder = installFolder is null ? null : Path.TrimEndingDirectorySeparator(Path.GetFullPath(installFolder)) + Path.DirectorySeparatorChar;

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (process.Id != Environment.ProcessId && (IsApp(process) || RunsFrom(process, folder)))
                {
                    Close(process);
                }
            }
        }
    }

    private static bool IsApp(Process process) =>
        string.Equals(process.ProcessName, InstallLayout.AppProcessName, StringComparison.OrdinalIgnoreCase);

    private static bool RunsFrom(Process process, string? folder)
    {
        if (folder is null)
        {
            return false;
        }

        try
        {
            return process.MainModule?.FileName is { } path && path.StartsWith(folder, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            // Windows' own protected processes can't be looked at, and never run from the install folder.
            return false;
        }
    }

    private static void Close(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(ExitTimeout);
        }
        catch (Exception)
        {
            // Already closed.
        }
    }
}

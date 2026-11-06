using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using BluetoothAudioManager.Shared.Helpers;

namespace BluetoothAudioManager.Installer.Services;

/// <summary>Sets up and removes the updater's Windows service, which starts with Windows and runs with administrator rights.</summary>
internal static class UpdaterService
{
    private const string Description = "Keeps Bluetooth Audio Manager up to date. Removed when the app is uninstalled.";

    private static readonly TimeSpan ServiceTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Sets up the service for the updater in the install folder and starts it.</summary>
    public static void Install(string installFolder)
    {
        var exe = Path.Combine(InstallLayout.UpdaterFolder(installFolder), InstallLayout.UpdaterExeName);

        RunSc("create", InstallLayout.ServiceName, "binPath=", $"\"{exe}\"", "start=", "auto", "DisplayName=", InstallLayout.ServiceDisplayName);
        RunSc("description", InstallLayout.ServiceName, Description);

        // Started again by Windows if it ever stops unexpectedly.
        RunSc("failure", InstallLayout.ServiceName, "reset=", "86400", "actions=", "restart/60000/restart/60000/restart/60000");

        Start();
    }

    /// <summary>Stops and removes the service, if it's there.</summary>
    public static void Remove()
    {
        try
        {
            Stop();
        }
        catch (InvalidOperationException)
        {
            // Not installed.
            return;
        }
        catch (System.ServiceProcess.TimeoutException)
        {
            // Removed anyway below, and gone once it stops.
        }

        RunSc("delete", InstallLayout.ServiceName);
    }

    /// <summary>Stops the service and waits until its program has closed, so its files can be replaced.</summary>
    public static void Stop()
    {
        using var service = new ServiceController(InstallLayout.ServiceName);
        if (service.Status != ServiceControllerStatus.Stopped)
        {
            service.Stop();
            service.WaitForStatus(ServiceControllerStatus.Stopped, ServiceTimeout);
        }

        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(InstallLayout.UpdaterExeName)))
        {
            using (process)
            {
                process.WaitForExit(ServiceTimeout);
            }
        }
    }

    public static void Start()
    {
        using var service = new ServiceController(InstallLayout.ServiceName);
        service.Start();
        service.WaitForStatus(ServiceControllerStatus.Running, ServiceTimeout);
    }

    private static void RunSc(params string[] arguments)
    {
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "sc.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
        };

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Setting up the updater service failed: {output.Trim()}");
        }
    }
}

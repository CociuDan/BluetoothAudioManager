using System.Diagnostics;
using System.ServiceProcess;
using BluetoothAudioManager.Shared.Helpers;

namespace BluetoothAudioManager.Updater.Services;

/// <summary>
/// Runs from a staged copy of a newer updater: stops the service, copies these files over the installed updater,
/// and starts the service again.
/// </summary>
internal static class ServiceReplacer
{
    public const string Argument = "--replace-service";

    private static readonly TimeSpan ServiceTimeout = TimeSpan.FromMinutes(1);

    public static void Run(string installFolder)
    {
        try
        {
            using var service = new ServiceController(InstallLayout.ServiceName);
            if (service.Status != ServiceControllerStatus.Stopped)
            {
                service.Stop();
                service.WaitForStatus(ServiceControllerStatus.Stopped, ServiceTimeout);
            }

            WaitForOtherUpdatersToClose();
            CopyFolder(AppContext.BaseDirectory, InstallLayout.UpdaterFolder(installFolder));

            service.Start();
            UpdaterLog.Write("Replaced the updater.");
        }
        catch (Exception exception)
        {
            UpdaterLog.Write($"Couldn't replace the updater: {exception.Message}");

            // The old updater is still there if the copy didn't happen, so it keeps running.
            try
            {
                using var service = new ServiceController(InstallLayout.ServiceName);
                service.Start();
            }
            catch (Exception)
            {
                // Windows starts it again at the next restart.
            }
        }
    }

    private static void WaitForOtherUpdatersToClose()
    {
        var self = Environment.ProcessId;
        var name = Path.GetFileNameWithoutExtension(InstallLayout.UpdaterExeName);

        foreach (var process in Process.GetProcessesByName(name).Where(process => process.Id != self))
        {
            using (process)
            {
                process.WaitForExit(ServiceTimeout);
            }
        }
    }

    private static void CopyFolder(string from, string to)
    {
        Directory.CreateDirectory(to);

        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }
}

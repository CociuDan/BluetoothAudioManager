using System.IO;
using System.IO.Compression;
using BluetoothAudioManager.Shared.Helpers;

namespace BluetoothAudioManager.Installer.Services;

/// <summary>
/// The installer's prepare mode, run by the updater service in the background after each download. It unpacks this
/// version's app into the hidden .update folder, so that updating later only swaps folders and opens the app, without
/// waiting for this installer to start and unpack.
/// </summary>
internal static class UpdatePreparation
{
    public static void Run()
    {
        if (InstallLayout.InstallFolder is not { } installFolder)
        {
            return;
        }

        var staged = InstallLayout.StagedFolder(installFolder);
        try
        {
            // Whatever was unpacked before is for an older download.
            InstalledFiles.DeleteFolder(staged);

            using (var package = AppInstaller.OpenPackage())
            using (var archive = new ZipArchive(package, ZipArchiveMode.Read))
            {
                PackageArchive.ExtractFolder(archive, InstallLayout.AppFolderName, InstallLayout.StagedAppFolder(installFolder));
            }

            // Written last, so a folder half unpacked is never taken as ready.
            File.WriteAllText(Path.Combine(staged, InstallLayout.StagedVersionFileName), AppInstaller.Version);
            UpdaterLog.Write($"Version {AppInstaller.Version} is unpacked and ready to swap in.");
        }
        catch (Exception exception)
        {
            // Updating then takes the longer way, unpacking as it goes.
            UpdaterLog.Write($"Couldn't unpack version {AppInstaller.Version} ahead of time: {exception.Message}");
            InstalledFiles.DeleteFolder(staged);
        }
    }
}

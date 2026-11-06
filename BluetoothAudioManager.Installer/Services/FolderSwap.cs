using System.IO;
using System.IO.Compression;
using BluetoothAudioManager.Shared.Helpers;

namespace BluetoothAudioManager.Installer.Services;

/// <summary>
/// Replaces an installed folder, such as the app, with the package's newer one. The new one is unpacked beside it first,
/// then the two are swapped by renaming, and the old one is put back if that fails.
/// </summary>
internal static class FolderSwap
{
    private const string NewSuffix = ".new";
    private const string OldSuffix = ".old";
    private const int MoveAttempts = 20;

    private static readonly TimeSpan RetryEvery = TimeSpan.FromMilliseconds(500);

    /// <summary>The leftovers a swap can leave beside the folder, if it was interrupted.</summary>
    public static IEnumerable<string> Leftovers(string folder) => [folder + NewSuffix, folder + OldSuffix];

    public static void Replace(ZipArchive archive, string packageFolderName, string folder)
    {
        var newFolder = folder + NewSuffix;
        var oldFolder = folder + OldSuffix;

        PackageArchive.ExtractFolder(archive, packageFolderName, newFolder);
        DeleteIfThere(oldFolder);

        if (Directory.Exists(folder))
        {
            Move(folder, oldFolder);
        }

        try
        {
            Move(newFolder, folder);
        }
        catch (Exception)
        {
            if (Directory.Exists(oldFolder))
            {
                Move(oldFolder, folder);
            }

            throw;
        }

        DeleteIfThere(oldFolder);
    }

    // Files can stay locked for a moment after a program closes, such as while antivirus looks at them.
    private static void Move(string from, string to)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                Directory.Move(from, to);
                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException && attempt < MoveAttempts)
            {
                Thread.Sleep(RetryEvery);
            }
        }
    }

    private static void DeleteIfThere(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception exception)
        {
            // Left for the next update to clear.
            UpdaterLog.Write($"Couldn't delete {folder}: {exception.Message}");
        }
    }
}

using System.IO.Compression;

namespace BluetoothAudioManager.Shared.Helpers;

/// <summary>
/// A release package: a zip with the app in its app folder and the updater in its updater folder,
/// as the installer and the updater both install it.
/// </summary>
public static class PackageArchive
{
    /// <summary>
    /// Extracts one of the package's folders, such as app, into the destination, which is emptied first.
    /// Entries that would land outside it are refused.
    /// </summary>
    /// <param name="progress">Called with the share of the folder done so far, from 0 to 1.</param>
    public static void ExtractFolder(ZipArchive archive, string folderName, string destination, Action<double>? progress = null)
    {
        if (Directory.Exists(destination))
        {
            Directory.Delete(destination, recursive: true);
        }

        Directory.CreateDirectory(destination);

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination)) + Path.DirectorySeparatorChar;
        var prefix = folderName + "/";
        var entries = archive.Entries
            .Where(entry => entry.FullName.Replace('\\', '/').StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (entries.Count == 0)
        {
            throw new InvalidDataException($"The package has no {folderName} folder.");
        }

        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            var relative = entry.FullName.Replace('\\', '/')[prefix.Length..];
            var target = Path.GetFullPath(Path.Combine(root, relative));

            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"The package has a file outside its {folderName} folder.");
            }

            // Folders are listed with a trailing slash and no data.
            if (relative.Length == 0 || relative.EndsWith('/'))
            {
                Directory.CreateDirectory(target);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                entry.ExtractToFile(target, overwrite: true);
            }

            progress?.Invoke((index + 1) / (double)entries.Count);
        }
    }
}

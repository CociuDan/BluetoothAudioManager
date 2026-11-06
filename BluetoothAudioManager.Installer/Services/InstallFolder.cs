using System.IO;
using BluetoothAudioManager.Shared.Helpers;

namespace BluetoothAudioManager.Installer.Services;

/// <summary>Chooses and checks the folder the app is installed in.</summary>
internal static class InstallFolder
{
    /// <summary>The folder it's installed in already, or otherwise Program Files.</summary>
    public static string Suggested() =>
        InstallLayout.InstallFolder
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), InstallLayout.DefaultFolderName);

    /// <summary>
    /// The folder a picked folder means: the app gets a folder of its own inside it, unless the picked one is already it.
    /// </summary>
    public static string ForPicked(string picked) =>
        string.Equals(Path.GetFileName(Path.TrimEndingDirectorySeparator(picked)), InstallLayout.DefaultFolderName, StringComparison.OrdinalIgnoreCase)
            ? picked
            : Path.Combine(picked, InstallLayout.DefaultFolderName);

    /// <summary>Why the app can't be installed in the folder, or null if it can.</summary>
    public static string? Problem(string folder)
    {
        try
        {
            if (!Path.IsPathFullyQualified(folder))
            {
                return @"Enter a full folder path, such as C:\Program Files\BluetoothAudioManager.";
            }

            var fullPath = Path.GetFullPath(folder);
            if (Path.GetPathRoot(fullPath) is not { } root || !Directory.Exists(root))
            {
                return "That drive isn't available. Choose another folder.";
            }

            // The app gets a folder of its own, so uninstalling never leaves a drive's top folder half emptied.
            if (string.Equals(Path.TrimEndingDirectorySeparator(fullPath), Path.TrimEndingDirectorySeparator(root), StringComparison.OrdinalIgnoreCase))
            {
                return "Choose a folder, not a whole drive.";
            }

            return null;
        }
        catch (Exception)
        {
            return "That isn't a folder path. Choose another one.";
        }
    }
}

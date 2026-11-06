using System.IO;
using BluetoothAudioManager.Shared.Helpers;
using Microsoft.Win32;

namespace BluetoothAudioManager.Installer.Services;

/// <summary>
/// Removes what the app keeps for each person on the PC: their settings, Start with Windows, and what Windows stores
/// for its notifications. People who aren't signed in have their registry files opened to reach theirs.
/// </summary>
internal static class UserCleanup
{
    private const string ProfileListPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "BluetoothAudioManager";
    private const string NotificationSettingsPath = @"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings";

    // Local accounts and Microsoft accounts. Others, such as Windows' own service accounts, never run the app.
    private const string PersonSidPrefix = "S-1-5-21-";

    public static void RemoveForEveryone()
    {
        foreach (var (sid, profile) in Profiles())
        {
            try
            {
                RemoveFor(sid, profile);
            }
            catch (Exception)
            {
                // One person's files that can't be reached don't stop the others being cleaned.
            }
        }
    }

    private static IEnumerable<(string Sid, string Folder)> Profiles()
    {
        using var machine = InstallLayout.OpenMachineKey();
        using var list = machine.OpenSubKey(ProfileListPath);
        if (list is null)
        {
            yield break;
        }

        foreach (var sid in list.GetSubKeyNames().Where(sid => sid.StartsWith(PersonSidPrefix, StringComparison.Ordinal)))
        {
            using var profile = list.OpenSubKey(sid);
            if (profile?.GetValue("ProfileImagePath") is string path
                && Environment.ExpandEnvironmentVariables(path) is var folder
                && Directory.Exists(folder))
            {
                yield return (sid, folder);
            }
        }
    }

    private static void RemoveFor(string sid, string profile)
    {
        DeleteFolder(Path.Combine(profile, "AppData", "Roaming", InstallLayout.DefaultFolderName));

        List<string> notificationIds = [];
        using var users = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Registry64);
        using var signedIn = users.OpenSubKey(sid, writable: true);

        if (signedIn is not null)
        {
            using (var classes = signedIn.OpenSubKey(@"Software\Classes", writable: true))
            {
                notificationIds = classes is null ? [] : RemoveNotificationRegistrations(classes);
            }

            RemoveSettings(signedIn, notificationIds);
        }
        else
        {
            // Someone who isn't signed in: their classes and the rest of their registry are in separate files.
            using (var classes = UserHive.Open(
                       Path.Combine(profile, "AppData", "Local", "Microsoft", "Windows", "UsrClass.dat"), $"BluetoothAudioManager_{sid}_Classes"))
            {
                if (classes is not null)
                {
                    notificationIds = RemoveNotificationRegistrations(classes.Root);
                }
            }

            using var hive = UserHive.Open(Path.Combine(profile, "NTUSER.DAT"), $"BluetoothAudioManager_{sid}");
            if (hive is not null)
            {
                RemoveSettings(hive.Root, notificationIds);
            }
        }

        foreach (var id in notificationIds)
        {
            DeleteFolder(Path.Combine(profile, "AppData", "Local", "ToastNotificationManagerCompat", "Apps", id));
        }
    }

    /// <summary>
    /// Removes the app's registrations for notifications with buttons, made by Microsoft's notification library
    /// the first time it showed one, and returns the app IDs they used.
    /// </summary>
    private static List<string> RemoveNotificationRegistrations(RegistryKey classes)
    {
        using var ids = classes.OpenSubKey("AppUserModelId", writable: true);
        if (ids is null)
        {
            return [];
        }

        List<string> removed = [];
        foreach (var id in ids.GetSubKeyNames())
        {
            string? activator;
            bool isOurs;
            using (var key = ids.OpenSubKey(id))
            {
                activator = key?.GetValue("CustomActivator") as string;
                isOurs = key?.GetValue("DisplayName") as string == InstallLayout.ProductName || StartsTheApp(classes, activator);
            }

            if (!isOurs)
            {
                continue;
            }

            ids.DeleteSubKeyTree(id, throwOnMissingSubKey: false);
            if (activator is not null)
            {
                classes.DeleteSubKeyTree($@"CLSID\{activator}", throwOnMissingSubKey: false);
            }

            removed.Add(id);
        }

        return removed;
    }

    // The registration names the program Windows starts when a notification is clicked.
    private static bool StartsTheApp(RegistryKey classes, string? activator)
    {
        if (activator is null)
        {
            return false;
        }

        using var server = classes.OpenSubKey($@"CLSID\{activator}\LocalServer32");
        return server?.GetValue(null) is string command
            && command.Contains(InstallLayout.AppExeName, StringComparison.OrdinalIgnoreCase);
    }

    private static void RemoveSettings(RegistryKey user, IEnumerable<string> notificationIds)
    {
        using (var run = user.OpenSubKey(RunKeyPath, writable: true))
        {
            run?.DeleteValue(RunValueName, throwOnMissingValue: false);
        }

        using var settings = user.OpenSubKey(NotificationSettingsPath, writable: true);
        foreach (var id in notificationIds)
        {
            settings?.DeleteSubKeyTree(id, throwOnMissingSubKey: false);
        }
    }

    // Waits a little for locked files, and leaves any still locked for Windows to delete when it restarts.
    private static void DeleteFolder(string folder) => InstalledFiles.DeleteFolder(folder);
}

using System.Security.AccessControl;
using System.Security.Principal;

namespace BluetoothAudioManager.Shared.Helpers;

/// <summary>
/// Folders only administrators and Windows itself can change, such as where the updater keeps downloads.
/// Anyone else can read them, but nobody can swap in a file the updater would then install with administrator rights.
/// </summary>
public static class SecureFolder
{
    /// <summary>Creates the folder, or locks down one that's there, with these rights only. Folders and files inside follow them.</summary>
    public static void Ensure(string path)
    {
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

        // Owned by administrators too, as whoever owns a folder can change who may use it,
        // and anyone can create a folder in ProgramData before the app is installed.
        security.SetOwner(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null));

        const InheritanceFlags inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null), FileSystemRights.ReadAndExecute, inherit, PropagationFlags.None, AccessControlType.Allow));

        var directory = new DirectoryInfo(path);
        if (directory.Exists)
        {
            directory.SetAccessControl(security);
        }
        else
        {
            directory.Create(security);
        }
    }
}

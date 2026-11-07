namespace BluetoothAudioManager.Shared.Helpers;

/// <summary>
/// A short log of what the updater did, in C:\ProgramData\BluetoothAudioManager\updater.log, for checking that updates work.
/// It's started afresh once it grows past a limit.
/// </summary>
public static class UpdaterLog
{
    private const long MaxBytes = 512 * 1024;

    private static readonly object Gate = new();

    private static string FilePath => Path.Combine(InstallLayout.DataFolder, "updater.log");

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                var file = new FileInfo(FilePath);
                if (file.Exists && file.Length > MaxBytes)
                {
                    file.Delete();
                }

                // To the millisecond, so how long each step of an update takes can be read from it.
                File.AppendAllText(FilePath, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
        }
        catch (Exception)
        {
            // Logging must never stop an update.
        }
    }
}

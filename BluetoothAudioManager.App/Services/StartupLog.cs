using System.Diagnostics;
using System.IO;

namespace BluetoothAudioManager.App.Services;

/// <summary>
/// How long the app takes to start, step by step, in %LocalAppData%\BluetoothAudioManager\startup.log, timed from when
/// Windows started the process. Its clock matches the updater's log, so the two can be read side by side after an update.
/// It's started afresh once it grows past a limit.
/// </summary>
internal static class StartupLog
{
    private const long MaxBytes = 256 * 1024;

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BluetoothAudioManager",
        "startup.log");

    private static readonly object Gate = new();

    /// <summary>Notes a step, with how long after the process started it was reached.</summary>
    public static void Mark(string step)
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            var elapsed = DateTime.Now - process.StartTime;
            Write($"{step}, {(int)elapsed.TotalMilliseconds} ms after start.");
        }
        catch (Exception)
        {
            // Timing must never stop the app.
        }
    }

    /// <summary>Notes something with only the time it happened, such as Update now being chosen.</summary>
    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                var file = new FileInfo(FilePath);
                if (file.Exists && file.Length > MaxBytes)
                {
                    file.Delete();
                }

                File.AppendAllText(FilePath, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
        }
        catch (Exception)
        {
            // Timing must never stop the app.
        }
    }
}

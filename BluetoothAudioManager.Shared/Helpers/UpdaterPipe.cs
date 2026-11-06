using System.IO.Pipes;
using System.Text;
using BluetoothAudioManager.Shared.Enums;
using BluetoothAudioManager.Shared.Models;

namespace BluetoothAudioManager.Shared.Helpers;

/// <summary>
/// How the app talks to the updater service: one line asked, one line answered, over a local named pipe.
/// The app can only ask for the status, a check, the install of what the updater downloaded and checked itself,
/// or whether it checks by itself, so nothing can use the updater to run files of its own choosing.
/// </summary>
public static class UpdaterPipe
{
    public const string PipeName = "BluetoothAudioManager.Updater";

    /// <summary>Answers with the status.</summary>
    public const string StatusCommand = "status";

    /// <summary>Checks GitHub now, downloads any update, then answers with the status.</summary>
    public const string CheckCommand = "check";

    /// <summary>Installs the ready update once the app closes, then opens it again. Answers ok, or with the status if nothing is ready.</summary>
    public const string InstallCommand = "install";

    /// <summary>Added after install, to open the app again with its window showing.</summary>
    public const string ShowWindowOption = "window";

    /// <summary>
    /// Alone, answers whether the updater checks for updates by itself: on or off.
    /// Followed by on or off, changes it for everyone on the PC, and answers ok.
    /// </summary>
    public const string AutomaticCommand = "automatic";

    public const string OnOption = "on";
    public const string OffOption = "off";

    public const string OkReply = "ok";

    private const string NoneReply = "none";
    private const string ReadyReply = "ready";
    private const string FailedReply = "failed";

    public static string Format(UpdateStatus status) => status.State switch
    {
        UpdateState.Ready => $"{ReadyReply} {status.Version}",
        UpdateState.None => NoneReply,
        _ => FailedReply,
    };

    public static UpdateStatus Parse(string? reply)
    {
        var parts = (reply ?? "").Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts switch
        {
            [ReadyReply, var version] => new UpdateStatus(UpdateState.Ready, version),
            [NoneReply] => new UpdateStatus(UpdateState.None),
            _ => new UpdateStatus(UpdateState.Failed),
        };
    }

    /// <summary>Sends one line to the updater and returns its answer, or null if the updater isn't there or doesn't answer in time.</summary>
    public static async Task<string?> SendAsync(string line, TimeSpan timeout)
    {
        using var cancel = new CancellationTokenSource(timeout);
        try
        {
            await using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(cancel.Token);

            using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
            await using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
            await writer.WriteLineAsync(line.AsMemory(), cancel.Token);
            return await reader.ReadLineAsync(cancel.Token);
        }
        catch (Exception)
        {
            return null;
        }
    }
}

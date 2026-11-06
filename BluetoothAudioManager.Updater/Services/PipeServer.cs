using System.Diagnostics;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using BluetoothAudioManager.Shared.Helpers;
using BluetoothAudioManager.Updater.Interop;
using BluetoothAudioManager.Updater.Models;

namespace BluetoothAudioManager.Updater.Services;

/// <summary>Answers the app's questions over the named pipe, one line in and one line out per connection.</summary>
internal static class PipeServer
{
    private const int MaxLineLength = 100;

    /// <summary>Accepts connections until stopped, handing each line and who sent it to the handler.</summary>
    public static async Task RunAsync(Func<string, PipeClient, CancellationToken, Task<string>> handle, CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe = null;
            try
            {
                pipe = Create();
                await pipe.WaitForConnectionAsync(stop);

                // Each connection is answered on its own, so a long check doesn't hold up a quick status question.
                var connected = pipe;
                pipe = null;
                _ = Task.Run(() => AnswerAsync(connected, handle, stop), stop);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                UpdaterLog.Write($"The pipe failed: {exception.Message}");
                await Task.Delay(TimeSpan.FromSeconds(5), stop);
            }
            finally
            {
                pipe?.Dispose();
            }
        }
    }

    // Anyone signed in may ask; only administrators and Windows itself may answer under this name.
    private static NamedPipeServerStream Create()
    {
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null), PipeAccessRights.ReadWrite, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            UpdaterPipe.PipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0, security);
    }

    private static async Task AnswerAsync(
        NamedPipeServerStream pipe, Func<string, PipeClient, CancellationToken, Task<string>> handle, CancellationToken stop)
    {
        try
        {
            await using (pipe)
            {
                using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
                await using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };

                var line = await reader.ReadLineAsync(stop);
                if (line is null || line.Length > MaxLineLength)
                {
                    return;
                }

                var reply = await handle(line.Trim(), IdentifyClient(pipe), stop);
                await writer.WriteLineAsync(reply.AsMemory(), stop);
            }
        }
        catch (Exception)
        {
            // The app gave up waiting or closed: nothing to answer.
        }
    }

    private static PipeClient IdentifyClient(NamedPipeServerStream pipe)
    {
        if (!NativeMethods.GetNamedPipeClientProcessId(pipe.SafePipeHandle, out var processId)
            || !NativeMethods.ProcessIdToSessionId(processId, out var sessionId))
        {
            return new PipeClient(uint.MaxValue, null);
        }

        string? path = null;
        try
        {
            using var process = Process.GetProcessById((int)processId);
            path = process.MainModule?.FileName;
        }
        catch (Exception)
        {
            // Unknown programs can still ask for the status.
        }

        return new PipeClient(sessionId, path);
    }
}

using Windows.Media.Control;

namespace BluetoothAudioManager.Core.Services;

/// <summary>
/// Controls the apps playing media. Every app is checked, because the one Windows
/// treats as current can be an idle app while another one is actually playing.
/// </summary>
public static class MediaSession
{
    /// <returns>The apps that were playing and have now been paused.</returns>
    public static async Task<IReadOnlyList<string>> PauseAllPlayingAsync()
    {
        var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        var paused = new List<string>();

        foreach (var session in manager.GetSessions())
        {
            if (session.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing
                && await session.TryPauseAsync())
            {
                paused.Add(session.SourceAppUserModelId);
            }
        }

        return paused;
    }

    public static async Task PlayAsync(IReadOnlyList<string> apps)
    {
        var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();

        foreach (var session in manager.GetSessions())
        {
            if (apps.Contains(session.SourceAppUserModelId))
            {
                await session.TryPlayAsync();
            }
        }
    }
}

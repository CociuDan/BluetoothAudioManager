using System.Windows.Threading;
using BluetoothAudioManager.Shared.Enums;
using BluetoothAudioManager.Shared.Helpers;
using BluetoothAudioManager.Shared.Models;

namespace BluetoothAudioManager.App.Services;

/// <summary>
/// Asks the updater service about updates. The service checks GitHub, downloads and installs them,
/// as only it can change Program Files. A build that wasn't installed has no service to ask.
/// </summary>
internal sealed class AppUpdater : IDisposable
{
    private static readonly TimeSpan AskEvery = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan QuickTimeout = TimeSpan.FromSeconds(3);

    // A check downloads any new version before answering, which can take a while.
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromMinutes(10);

    private readonly DispatcherTimer _timer = new() { Interval = AskEvery };
    private string? _announced;

    public AppUpdater()
    {
        _timer.Tick += (_, _) => _ = AnnounceStatusAsync();
    }

    /// <summary>Raised with the new version once the updater has it downloaded and ready to install.</summary>
    public event Action<string>? UpdateReady;

    /// <summary>Whether this is the installed app, which the updater keeps up to date.</summary>
    public bool IsInstalled { get; } = InstallLayout.IsInstalledApp();

    /// <summary>The installed version, such as 0.0.3, or null for a build that wasn't installed.</summary>
    public string? CurrentVersion => IsInstalled ? InstallLayout.InstalledVersion : null;

    /// <summary>
    /// Asks straight away whether an update is ready, waiting only briefly. Used as the app starts,
    /// so an update chosen for later installs before the window opens.
    /// </summary>
    public string? ReadyVersionNow()
    {
        if (!IsInstalled)
        {
            return null;
        }

        var status = Task.Run(StatusAsync).GetAwaiter().GetResult();
        return status is { State: UpdateState.Ready, Version: { } version } ? version : null;
    }

    /// <summary>Starts asking the updater regularly whether it has a new version ready.</summary>
    public void Start()
    {
        if (!IsInstalled)
        {
            return;
        }

        _timer.Start();
        _ = AnnounceStatusAsync();
    }

    /// <summary>Asks the updater to check GitHub now. A new version found is announced as usual.</summary>
    public async Task<UpdateStatus> CheckNowAsync()
    {
        var status = UpdaterPipe.Parse(await UpdaterPipe.SendAsync(UpdaterPipe.CheckCommand, CheckTimeout));
        Announce(status);
        return status;
    }

    /// <summary>
    /// Asks the updater to install the ready version. It waits for the app to close, then opens it again,
    /// so the caller closes the app straight after this returns true.
    /// </summary>
    /// <param name="showWindow">Whether the window opens again, as it was showing.</param>
    public async Task<bool> InstallAsync(bool showWindow)
    {
        var command = showWindow ? $"{UpdaterPipe.InstallCommand} {UpdaterPipe.ShowWindowOption}" : UpdaterPipe.InstallCommand;
        return await UpdaterPipe.SendAsync(command, QuickTimeout) == UpdaterPipe.OkReply;
    }

    private static async Task<UpdateStatus> StatusAsync() =>
        UpdaterPipe.Parse(await UpdaterPipe.SendAsync(UpdaterPipe.StatusCommand, QuickTimeout));

    private async Task AnnounceStatusAsync() => Announce(await StatusAsync());

    // Each new version is announced once.
    private void Announce(UpdateStatus status)
    {
        if (status is { State: UpdateState.Ready, Version: { } version } && version != _announced)
        {
            _announced = version;
            UpdateReady?.Invoke(version);
        }
    }

    public void Dispose() => _timer.Stop();
}

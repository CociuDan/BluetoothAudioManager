using System.Reflection;
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

    // Holding up the window only briefly: an update the updater doesn't confirm in time is offered a minute later.
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(1);

    // A check downloads any new version before answering, which can take a while.
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ReleaseNotesTimeout = TimeSpan.FromSeconds(15);

    // Dismissing waits for any check under way, then deletes the prepared files.
    private static readonly TimeSpan DismissTimeout = TimeSpan.FromSeconds(30);

    private readonly DispatcherTimer _timer = new() { Interval = AskEvery };
    private string? _announced;
    private UpdateStatus? _readyStatus;

    public AppUpdater()
    {
        _timer.Tick += (_, _) => _ = AnnounceStatusAsync();
    }

    /// <summary>Raised with the new version once the updater has it downloaded and ready to install.</summary>
    public event Action<string>? UpdateReady;

    /// <summary>Whether this is the installed app, which the updater keeps up to date.</summary>
    public bool IsInstalled { get; } = InstallLayout.IsInstalledApp();

    /// <summary>
    /// The installed version, such as 0.0.3, or null for a build that wasn't installed. Read from the app itself,
    /// as just after an update the registry gets the new version a moment after the app opens.
    /// </summary>
    public string? CurrentVersion =>
        IsInstalled ? Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? InstallLayout.InstalledVersion : null;

    /// <summary>
    /// Asks straight away whether an update is ready, waiting only a moment. Used as the app starts,
    /// so an update chosen for later installs before the window opens.
    /// </summary>
    public string? ReadyVersionNow()
    {
        if (!IsInstalled)
        {
            return null;
        }

        var status = Task.Run(async () =>
            UpdaterPipe.Parse(await UpdaterPipe.SendAsync(UpdaterPipe.StatusCommand, StartupTimeout))).GetAwaiter().GetResult();
        if (status.State == UpdateState.Ready)
        {
            _readyStatus = status;
        }

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

    /// <summary>
    /// Asks the updater to check GitHub now. A new version found is announced as usual, and so is one that was
    /// dismissed, as asking for a check offers it again.
    /// </summary>
    public async Task<UpdateStatus> CheckNowAsync()
    {
        var status = UpdaterPipe.Parse(await UpdaterPipe.SendAsync(UpdaterPipe.CheckCommand, CheckTimeout));
        _announced = null;
        Announce(status);
        return status;
    }

    /// <summary>
    /// Dismisses the ready version: the updater deletes its files and doesn't offer it again by itself.
    /// Returns whether the updater did it. If not, the version is announced again, as it's still there.
    /// </summary>
    public async Task<bool> DismissAsync(string version)
    {
        _readyStatus = null;
        var dismissed = IsInstalled
            && await UpdaterPipe.SendAsync($"{UpdaterPipe.DismissCommand} {version}", DismissTimeout) == UpdaterPipe.OkReply;
        if (!dismissed)
        {
            _announced = null;
        }

        return dismissed;
    }

    /// <summary>
    /// Asks the updater how far it is right now, such as preparing a new version for install, without waiting for it.
    /// A build that wasn't installed has nothing to ask.
    /// </summary>
    public async Task<UpdateStatus> StatusNowAsync() =>
        IsInstalled ? await StatusAsync() : new UpdateStatus(UpdateState.None);

    /// <summary>Reads the latest published release notes through the updater, which has access to the private repository.</summary>
    public async Task<(string Version, string Notes)?> LatestReleaseNotesAsync()
    {
        if (!IsInstalled)
        {
            return null;
        }

        if (_readyStatus is { State: UpdateState.Ready, Version: { } version, Notes: { } notes })
        {
            return (version, notes);
        }

        var reply = await UpdaterPipe.SendAsync(UpdaterPipe.ReleaseNotesCommand, ReleaseNotesTimeout);
        return UpdaterPipe.ParseReleaseNotes(reply);
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

    /// <summary>Whether the updater checks for updates by itself, or null if it can't be asked, such as in a build that wasn't installed.</summary>
    public async Task<bool?> IsAutomaticAsync()
    {
        if (!IsInstalled)
        {
            return null;
        }

        return await UpdaterPipe.SendAsync(UpdaterPipe.AutomaticCommand, QuickTimeout) switch
        {
            UpdaterPipe.OnOption => true,
            UpdaterPipe.OffOption => false,
            _ => null,
        };
    }

    /// <summary>Turns checking by itself on or off, for everyone on the PC. Returns whether the updater did it.</summary>
    public async Task<bool> SetAutomaticAsync(bool on)
    {
        var command = $"{UpdaterPipe.AutomaticCommand} {(on ? UpdaterPipe.OnOption : UpdaterPipe.OffOption)}";
        return await UpdaterPipe.SendAsync(command, QuickTimeout) == UpdaterPipe.OkReply;
    }

    private static async Task<UpdateStatus> StatusAsync() =>
        UpdaterPipe.Parse(await UpdaterPipe.SendAsync(UpdaterPipe.StatusCommand, QuickTimeout));

    private async Task AnnounceStatusAsync() => Announce(await StatusAsync());

    // Each new version is announced once.
    private void Announce(UpdateStatus status)
    {
        _readyStatus = status.State == UpdateState.Ready ? status : null;
        if (status is { State: UpdateState.Ready, Version: { } version } && version != _announced)
        {
            _announced = version;
            UpdateReady?.Invoke(version);
        }
    }

    public void Dispose() => _timer.Stop();
}

using System.Windows;
using System.Windows.Threading;
using BluetoothAudioManager.App.Services;
using BluetoothAudioManager.App.Views;
using BluetoothAudioManager.Shared.Enums;

namespace BluetoothAudioManager.App;

/// <summary>Runs the app from the system tray, keeping a single copy open and up to date.</summary>
public partial class App : Application
{
    private const string InstanceName = "BluetoothAudioManager.SingleInstance";
    private const string ShowRequestName = "BluetoothAudioManager.ShowRequest";

    private static readonly TimeSpan PausedMediaCheck = TimeSpan.FromSeconds(1);

    private Mutex? _instance;
    private EventWaitHandle? _showRequest;
    private RegisteredWaitHandle? _showRequestWait;
    private MainWindow? _window;
    private TrayIcon? _trayIcon;
    private AppUpdater? _updater;
    private UpdateNotification? _updateNotification;
    private DispatcherTimer? _updateWait;
    private bool _installing;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Windows starts the installed app for everyone; someone who turned that off gets nothing.
        var fromWindowsStartup = e.Args.Contains(StartupRegistration.StartupArgument);
        if (fromWindowsStartup && !StartupRegistration.IsEnabled)
        {
            Shutdown();
            return;
        }

        StartupRegistration.RemoveOwnEntryIfInstalled();

        _instance = new Mutex(true, InstanceName, out var isFirstCopy);
        _showRequest = new EventWaitHandle(false, EventResetMode.AutoReset, ShowRequestName);

        if (!isFirstCopy)
        {
            _showRequest.Set();
            Shutdown();
            return;
        }

        var showWindow = !fromWindowsStartup && !e.Args.Contains(StartupRegistration.TrayArgument);
        _updater = new AppUpdater();

        // An update chosen for later installs now, before the window opens. The updater opens the app again afterwards.
        if (_updater.ReadyVersionNow() is not null
            && Task.Run(() => _updater.InstallAsync(showWindow)).GetAwaiter().GetResult())
        {
            Shutdown();
            return;
        }

        _window = new MainWindow();
        _window.UseUpdater(_updater);
        _trayIcon = new TrayIcon(_window.Toggle, ExitApp, InstallUpdate, CheckForUpdates, _updater.CurrentVersion);
        _updateNotification = new UpdateNotification(InstallUpdate, _window.ShowInCorner);
        _window.LowBatteryAlert += _trayIcon.ShowNotification;
        _window.UpdateClicked += InstallUpdate;
        _updater.UpdateReady += OnUpdateReady;
        _window.StartMonitoring();
        _showRequestWait = ThreadPool.RegisterWaitForSingleObject(
            _showRequest,
            (_, _) => Dispatcher.BeginInvoke(() => _window.ShowInCorner()),
            null,
            Timeout.Infinite,
            false);

        if (showWindow)
        {
            _window.ShowInCorner();
        }

        _updater.Start();
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        base.OnSessionEnding(e);
        _window?.AllowClose();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _updateWait?.Stop();
        _showRequestWait?.Unregister(null);
        _updateNotification?.Dispose();
        _trayIcon?.Dispose();
        _updater?.Dispose();
        _showRequest?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }

    private void OnUpdateReady(string version)
    {
        _trayIcon?.ShowUpdate(version);
        _window?.ShowUpdateReady(version);
        // _updateNotification?.Show(version);
    }

    private async void CheckForUpdates()
    {
        if (_updater is null)
        {
            return;
        }

        var status = await _updater.CheckNowAsync();

        // A new version is announced by the tray menu and the window, as usual.
        switch (status.State)
        {
            case UpdateState.None:
                _trayIcon?.ShowInformation("No update", $"Version {_updater.CurrentVersion} is the latest.");
                break;
            case UpdateState.Failed:
                _trayIcon?.ShowInformation("Couldn't check for updates", "Check your internet connection and try again later.");
                break;
        }
    }

    /// <summary>
    /// Asks the updater to install the downloaded version, then closes, and the updater opens the app again.
    /// Only the app restarts: Windows keeps the headphones connected, the sound output and whatever is playing,
    /// as the app holds none of them.
    /// </summary>
    private async void InstallUpdate()
    {
        if (_window is null || _updater is null || _installing)
        {
            return;
        }

        // Restarting would forget media that ear detection paused, so it wouldn't resume when the pods go back in.
        // The update waits until it has.
        if (_window.HasPausedMedia)
        {
            if (_updateWait is null)
            {
                _updateWait = new DispatcherTimer { Interval = PausedMediaCheck };
                _updateWait.Tick += (_, _) => InstallUpdate();
                _updateWait.Start();
            }

            return;
        }

        _updateWait?.Stop();
        _updateWait = null;
        _installing = true;

        if (await _updater.InstallAsync(_window.IsVisible))
        {
            ExitApp();
            return;
        }

        _installing = false;
        _trayIcon?.ShowInformation("Couldn't update", "The updater didn't respond. Try again in a moment.");
    }

    private void ExitApp()
    {
        _window?.AllowClose();
        _window?.Close();
        Shutdown();
    }
}

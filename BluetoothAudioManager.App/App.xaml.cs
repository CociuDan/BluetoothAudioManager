using System.Windows;
using BluetoothAudioManager.App.Services;
using BluetoothAudioManager.App.Views;

namespace BluetoothAudioManager.App;

/// <summary>Runs the app from the system tray, keeping a single copy open.</summary>
public partial class App : Application
{
    private const string InstanceName = "BluetoothAudioManager.SingleInstance";
    private const string ShowRequestName = "BluetoothAudioManager.ShowRequest";

    private Mutex? _instance;
    private EventWaitHandle? _showRequest;
    private RegisteredWaitHandle? _showRequestWait;
    private MainWindow? _window;
    private TrayIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _instance = new Mutex(true, InstanceName, out var isFirstCopy);
        _showRequest = new EventWaitHandle(false, EventResetMode.AutoReset, ShowRequestName);

        if (!isFirstCopy)
        {
            _showRequest.Set();
            Shutdown();
            return;
        }

        _window = new MainWindow();
        _trayIcon = new TrayIcon(_window.Toggle, ExitApp);
        _window.LowBatteryAlert += _trayIcon.ShowNotification;
        _window.StartMonitoring();
        _showRequestWait = ThreadPool.RegisterWaitForSingleObject(
            _showRequest,
            (_, _) => Dispatcher.BeginInvoke(() => _window.ShowInCorner()),
            null,
            Timeout.Infinite,
            false);

        if (!e.Args.Contains(StartupRegistration.TrayArgument))
        {
            _window.ShowInCorner();
        }
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        base.OnSessionEnding(e);
        _window?.AllowClose();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _showRequestWait?.Unregister(null);
        _trayIcon?.Dispose();
        _showRequest?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }

    private void ExitApp()
    {
        _window?.AllowClose();
        _window?.Close();
        Shutdown();
    }
}

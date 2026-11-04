using System.Drawing;
using Forms = System.Windows.Forms;

namespace BluetoothAudioManager.App;

/// <summary>The app's icon in the system tray, with its right-click menu.</summary>
internal sealed class TrayIcon : IDisposable
{
    private const int NotificationMilliseconds = 5000;

    private static readonly Uri IconUri = new("pack://application:,,,/Assets/TrayIcon.ico");

    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ContextMenuStrip _menu;
    private readonly Icon _icon;

    public TrayIcon(Action onClick, Action onExit)
    {
        using var iconStream = System.Windows.Application.GetResourceStream(IconUri).Stream;
        _icon = new Icon(iconStream, Forms.SystemInformation.SmallIconSize);

        var startWithWindows = new Forms.ToolStripMenuItem("Start with Windows")
        {
            CheckOnClick = true,
            Checked = StartupRegistration.IsEnabled,
        };
        startWithWindows.Click += (_, _) => StartupRegistration.SetEnabled(startWithWindows.Checked);

        _menu = new Forms.ContextMenuStrip();
        _menu.Items.Add(startWithWindows);
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add("Exit", null, (_, _) => onExit());

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _icon,
            Text = "Bluetooth Audio Manager",
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
            {
                onClick();
            }
        };
    }

    public void ShowNotification(string title, string message) =>
        _notifyIcon.ShowBalloonTip(NotificationMilliseconds, title, message, Forms.ToolTipIcon.Warning);

    public void Dispose()
    {
        // Hiding it first stops a dead icon lingering in the tray until the mouse passes over it.
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _icon.Dispose();
    }
}

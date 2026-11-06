using System.Drawing;
using Forms = System.Windows.Forms;

namespace BluetoothAudioManager.App.Services;

/// <summary>The app's icon in the system tray, with its right-click menu.</summary>
internal sealed class TrayIcon : IDisposable
{
    private const int NotificationMilliseconds = 5000;

    private static readonly Uri IconUri = new("pack://application:,,,/Assets/TrayIcon.ico");

    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ContextMenuStrip _menu;
    private readonly Forms.ToolStripMenuItem _updateItem;
    private readonly Forms.ToolStripSeparator _updateSeparator;
    private readonly Font _updateFont;
    private readonly Icon _icon;

    /// <param name="onUpdate">Runs when the update item is clicked, once an update is ready.</param>
    /// <param name="onCheck">Runs when Check for updates is clicked.</param>
    /// <param name="version">The installed version, shown in the menu with Check for updates, or null to leave both out.</param>
    public TrayIcon(Action onClick, Action onExit, Action onUpdate, Action onCheck, string? version)
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

        // The window's settings can change it too, so it's read again each time the menu opens.
        _menu.Opening += (_, _) => startWithWindows.Checked = StartupRegistration.IsEnabled;

        // Hidden until an update is downloaded.
        _updateFont = new Font(_menu.Font, FontStyle.Bold);
        _updateItem = new Forms.ToolStripMenuItem { Font = _updateFont, Visible = false };
        _updateItem.Click += (_, _) => onUpdate();
        _updateSeparator = new Forms.ToolStripSeparator { Visible = false };

        _menu.Items.Add(_updateItem);
        _menu.Items.Add(_updateSeparator);
        _menu.Items.Add(startWithWindows);
        _menu.Items.Add(new Forms.ToolStripSeparator());
        if (version is not null)
        {
            _menu.Items.Add("Check for updates", null, (_, _) => onCheck());
            _menu.Items.Add(new Forms.ToolStripMenuItem($"Version {version}") { Enabled = false });
        }

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

    /// <summary>A notification that only informs, such as the result of checking for updates.</summary>
    public void ShowInformation(string title, string message) =>
        _notifyIcon.ShowBalloonTip(NotificationMilliseconds, title, message, Forms.ToolTipIcon.Info);

    /// <summary>Adds the item that installs the downloaded update, at the top of the menu.</summary>
    public void ShowUpdate(string version)
    {
        _updateItem.Text = $"Update to {version}";
        _updateItem.Visible = true;
        _updateSeparator.Visible = true;
    }

    public void Dispose()
    {
        // Hiding it first stops a dead icon lingering in the tray until the mouse passes over it.
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _updateFont.Dispose();
        _icon.Dispose();
    }
}

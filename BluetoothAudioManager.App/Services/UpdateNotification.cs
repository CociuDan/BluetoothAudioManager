using System.Windows;
using Microsoft.Toolkit.Uwp.Notifications;

namespace BluetoothAudioManager.App.Services;

/// <summary>The Windows notification for a downloaded update, with buttons to install it now or later.</summary>
internal sealed class UpdateNotification : IDisposable
{
    private const string ActionKey = "action";
    private const string UpdateAction = "update";
    private const string OpenAction = "open";

    private readonly OnActivated _onActivated;

    /// <param name="onUpdate">Runs when Update now is clicked.</param>
    /// <param name="onOpen">Runs when the rest of the notification is clicked.</param>
    public UpdateNotification(Action onUpdate, Action onOpen)
    {
        // Windows calls this on a background thread.
        _onActivated = e =>
        {
            var action = ToastArguments.Parse(e.Argument).TryGetValue(ActionKey, out var value) ? value : null;
            Application.Current.Dispatcher.BeginInvoke(action == UpdateAction ? onUpdate : onOpen);
        };
        ToastNotificationManagerCompat.OnActivated += _onActivated;
    }

    public void Show(string version) =>
        new ToastContentBuilder()
            .AddArgument(ActionKey, OpenAction)
            .AddText("Update ready")
            .AddText($"Version {version} is downloaded. Your headphones and sound stay as they are while it installs.")
            .AddButton(new ToastButton().SetContent("Update now").AddArgument(ActionKey, UpdateAction))
            .AddButton(new ToastButton().SetContent("Later").SetDismissActivation())
            .Show();

    public void Dispose() => ToastNotificationManagerCompat.OnActivated -= _onActivated;
}

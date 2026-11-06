using System.Windows;
using System.Windows.Input;
using BluetoothAudioManager.Installer.Services;

namespace BluetoothAudioManager.Installer.Views;

/// <summary>The uninstaller's first page. Uninstall shows the progress page, which closes once everything's removed.</summary>
public partial class UninstallWindow : Window
{
    public UninstallWindow()
    {
        InitializeComponent();
    }

    private async void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        var progress = new ProgressWindow("Uninstalling Bluetooth Audio Manager");
        Hide();
        progress.Show();

        Exception? failure = null;
        try
        {
            await Task.Run(() => AppUninstaller.Uninstall(progress.Progress));
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        progress.Close();
        if (failure is not null)
        {
            MessageBox.Show($"Bluetooth Audio Manager couldn't be fully removed: {failure.Message}",
                Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        AppUninstaller.DeleteSelfLater();
        Application.Current.Shutdown();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        AppUninstaller.DeleteSelfLater();
        Application.Current.Shutdown();
    }

    // The page has no title bar, so any empty part of it drags it.
    private void Page_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
}

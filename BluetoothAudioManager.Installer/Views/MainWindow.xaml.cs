using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BluetoothAudioManager.Installer.Services;
using Microsoft.Win32;

namespace BluetoothAudioManager.Installer.Views;

/// <summary>The installer's first page: where to install. Install then shows the progress page and opens the app.</summary>
public partial class MainWindow : Window
{
    private const string Note = "For everyone on this PC. Updates install here too, through the updater installed with the app.";

    private static readonly Brush NoteBrush = new SolidColorBrush(Color.FromRgb(0xCF, 0xCF, 0xCF));
    private static readonly Brush ProblemBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x99, 0xA4));

    public MainWindow()
    {
        InitializeComponent();

        // Setting it checks the folder and fills in the note.
        FolderBox.Text = InstallFolder.Suggested();
        Loaded += (_, _) => FolderBox.Focus();
    }

    private string Folder => FolderBox.Text.Trim();

    private void FolderBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var problem = InstallFolder.Problem(Folder);
        FolderNote.Text = problem ?? Note;
        FolderNote.Foreground = problem is null ? NoteBrush : ProblemBrush;
        InstallButton.IsEnabled = problem is null;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Choose where to install Bluetooth Audio Manager" };
        if (dialog.ShowDialog(this) == true)
        {
            FolderBox.Text = InstallFolder.ForPicked(dialog.FolderName);
        }
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (!AppInstaller.HasPackage)
        {
            MessageBox.Show(this, "This copy of the installer has nothing to install. Use the one from the latest release.",
                Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var folder = Folder;
        var progress = new ProgressWindow("Installing Bluetooth Audio Manager");
        Hide();
        progress.Show();

        try
        {
            await Task.Run(() => AppInstaller.Install(folder, progress.Progress));
        }
        catch (Exception exception)
        {
            progress.Close();
            Show();
            MessageBox.Show(this, $"Bluetooth Audio Manager couldn't be installed: {exception.Message}",
                Title, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        AppInstaller.OpenApp(folder);
        progress.Close();
        Application.Current.Shutdown();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    // The page has no title bar, so any empty part of it drags it.
    private void Page_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
}

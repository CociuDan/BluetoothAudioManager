using System.Windows;
using System.Windows.Input;

namespace BluetoothAudioManager.Installer.Views;

/// <summary>Shows how far installing or uninstalling has got.</summary>
public partial class ProgressWindow : Window
{
    private const double BarWidth = 280;

    /// <param name="message">Such as "Installing Bluetooth Audio Manager".</param>
    public ProgressWindow(string message)
    {
        InitializeComponent();
        MessageText.Text = message;
        Progress = new Progress<double>(share => Bar.Width = BarWidth * Math.Clamp(share, 0, 1));
    }

    /// <summary>Reports the share done, from 0 to 1, from any thread.</summary>
    public IProgress<double> Progress { get; }

    // The page has no title bar, so any part of it drags it.
    private void Page_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
}

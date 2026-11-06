using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BluetoothAudioManager.App.Models;
using BluetoothAudioManager.App.Services;

namespace BluetoothAudioManager.App.Views;

/// <summary>
/// The app's settings: starting with Windows, and how see-through the window is. Changes apply straight away and are saved.
/// </summary>
public partial class AppSettingsPanel : UserControl
{
    private AppSettings? _settings;
    private bool _showing;

    public AppSettingsPanel()
    {
        InitializeComponent();
    }

    /// <summary>Shows the current settings. Start with Windows is read again, as the tray menu can change it too.</summary>
    public void Show(AppSettings settings)
    {
        _settings = settings;
        _showing = true;
        StartupToggle.IsChecked = StartupRegistration.IsEnabled;
        TransparencySlider.Value = WindowBackdrop.ClampTransparency(settings.Transparency);
        _showing = false;
        UpdateText();
    }

    private void StartupToggle_Click(object sender, RoutedEventArgs e)
    {
        StartupRegistration.SetEnabled(StartupToggle.IsChecked == true);
        UpdateText();
    }

    private void TransparencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_settings is null || _showing)
        {
            return;
        }

        UpdateText();
        _settings.Transparency = (int)TransparencySlider.Value;
        _settings.Save();
        WindowBackdrop.Transparency = _settings.Transparency;
    }

    // The wheel moves the slider a step, rather than scrolling the window behind it.
    private void TransparencySlider_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        TransparencySlider.Value += Math.Sign(e.Delta) * TransparencySlider.TickFrequency;
        e.Handled = true;
    }

    private void UpdateText()
    {
        StartupText.Text = StartupToggle.IsChecked == true ? "On" : "Off";
        TransparencyText.Text = TransparencySlider.Value == 0 ? "Solid" : $"{TransparencySlider.Value:0}%";
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BluetoothAudioManager.App.Models;
using BluetoothAudioManager.App.Services;
using BluetoothAudioManager.Shared.Enums;

namespace BluetoothAudioManager.App.Views;

/// <summary>
/// The app's settings: starting with Windows, updates, and how see-through the window is. Changes apply straight away and are saved.
/// </summary>
public partial class AppSettingsPanel : UserControl
{
    private AppSettings? _settings;
    private bool _showing;
    private bool _checking;
    private bool _updateReady;

    public AppSettingsPanel()
    {
        InitializeComponent();
    }

    /// <summary>The updater to ask about checking automatically, set by the app. Without one, that setting isn't shown.</summary>
    internal AppUpdater? Updater { get; set; }

    /// <summary>Raised when the ready update is chosen for installation.</summary>
    public event Action? UpdateClicked;

    /// <summary>Raised when settings should return to the device view.</summary>
    public event Action? BackClicked;

    /// <summary>Raised when Check now finds a ready update.</summary>
    public event Action<string>? UpdateFoundByCheck;

    /// <summary>Shows the current settings. Start with Windows is read again, as the tray menu and Task Manager can change it too.</summary>
    public void Show(AppSettings settings)
    {
        _settings = settings;
        _showing = true;
        StartupToggle.IsChecked = StartupRegistration.IsEnabled;
        TransparencySlider.Value = WindowBackdrop.ClampTransparency(settings.Transparency);
        _showing = false;
        UpdateText();
        ShowAutomaticUpdates();
        ShowUpdates();
    }

    private void ShowUpdates()
    {
        UpdatesRow.Visibility = Updater?.IsInstalled == true && !_updateReady
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <summary>Changes the update action to installation once the updater has a ready version.</summary>
    public void ShowUpdateReady()
    {
        _updateReady = true;
        CheckNowButton.Content = "Update now";
        ShowUpdates();
    }

    private void BackButton_Click(object sender, RoutedEventArgs e) => BackClicked?.Invoke();

    private async void CheckNowButton_Click(object sender, RoutedEventArgs e)
    {
        if (Updater is null || _checking)
        {
            return;
        }

        if (_updateReady)
        {
            UpdateClicked?.Invoke();
            return;
        }

        _checking = true;
        CheckNowButton.IsEnabled = false;
        CheckNowButton.Content = "Checking…";

        var status = await Updater.CheckNowAsync();
        if (status is { State: UpdateState.Ready, Version: { } version })
        {
            ShowUpdateReady();
            UpdateFoundByCheck?.Invoke(version);
        }

        CheckNowButton.Content = _updateReady ? "Update now" : "Check now";
        CheckNowButton.IsEnabled = true;
        _checking = false;
    }

    // Asked of the updater each time, as someone else on the PC may have changed it. Hidden if it can't be asked.
    private async void ShowAutomaticUpdates()
    {
        var on = Updater is null ? null : await Updater.IsAutomaticAsync();
        AutomaticUpdatesRow.Visibility = on is null ? Visibility.Collapsed : Visibility.Visible;
        AutomaticUpdatesToggle.IsChecked = on == true;
        UpdateText();
    }

    private async void AutomaticUpdatesToggle_Click(object sender, RoutedEventArgs e)
    {
        var on = AutomaticUpdatesToggle.IsChecked == true;
        UpdateText();
        AutomaticUpdatesToggle.IsEnabled = false;

        // Put back if the updater didn't take it.
        if (Updater is null || !await Updater.SetAutomaticAsync(on))
        {
            AutomaticUpdatesToggle.IsChecked = !on;
            UpdateText();
        }

        AutomaticUpdatesToggle.IsEnabled = true;
    }

    private void StartupToggle_Click(object sender, RoutedEventArgs e)
    {
        // Shows what actually happened, as turning it on can't undo Task Manager turning it off for everyone.
        StartupToggle.IsChecked = StartupRegistration.SetEnabled(StartupToggle.IsChecked == true);
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
        AutomaticUpdatesText.Text = AutomaticUpdatesToggle.IsChecked == true ? "On" : "Off";
        TransparencyText.Text = TransparencySlider.Value == 0 ? "Solid" : $"{TransparencySlider.Value:0}%";
    }
}

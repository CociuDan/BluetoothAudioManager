using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BluetoothAudioManager.App.Models;
using BluetoothAudioManager.App.Services;
using BluetoothAudioManager.Shared.Enums;
using BluetoothAudioManager.Shared.Models;

namespace BluetoothAudioManager.App.Views;

/// <summary>
/// The app's settings: the theme, starting with the system, updates, and how see-through the window is. Changes apply straight away and are saved.
/// </summary>
public partial class AppSettingsPanel : UserControl
{
    private const string CheckingText = "Checking…";

    private static readonly TimeSpan StageCheckEvery = TimeSpan.FromMilliseconds(500);

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

    /// <summary>Raised after the theme changes, for anything that has to be swapped by hand, such as pictures.</summary>
    public event Action? ThemeChanged;

    /// <summary>Raised when the ready update is chosen for installation.</summary>
    public event Action? UpdateClicked;

    /// <summary>Raised when Check now finds a ready update.</summary>
    public event Action<string>? UpdateFoundByCheck;

    /// <summary>Shows the current settings. Start with the System is read again, as the tray menu and Task Manager can change it too.</summary>
    public void Show(AppSettings settings)
    {
        _settings = settings;
        _showing = true;
        ThemeToggle.IsChecked = settings.UseLightTheme;
        StartupToggle.IsChecked = StartupRegistration.IsEnabled;
        TransparencySlider.Value = WindowBackdrop.ClampTransparency(settings.Transparency);
        _showing = false;
        UpdateText();
        ShowAutomaticUpdates();
        ShowUpdates();
        FollowWorkInProgress();
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
        CheckNowButton.Content = CheckingText;

        // The check answers once a new version is downloaded and unpacked, so meanwhile the button follows how far it got.
        var check = Updater.CheckNowAsync();
        while (await Task.WhenAny(check, Task.Delay(StageCheckEvery)) != check)
        {
            ShowStage(await Updater.StatusNowAsync());
        }

        FinishWorking(await check, foundByCheck: true);
    }

    // A download the updater started by itself shows the same way, until it's ready.
    private async void FollowWorkInProgress()
    {
        if (Updater is null || _checking || _updateReady)
        {
            return;
        }

        var status = await Updater.StatusNowAsync();
        if (status.State is not (UpdateState.Downloading or UpdateState.Preparing))
        {
            return;
        }

        _checking = true;
        CheckNowButton.IsEnabled = false;
        while (status.State is UpdateState.Downloading or UpdateState.Preparing)
        {
            ShowStage(status);
            await Task.Delay(StageCheckEvery);
            status = await Updater.StatusNowAsync();
        }

        // Announced by the app's regular look at the updater, as it found it by itself.
        FinishWorking(status, foundByCheck: false);
    }

    private void ShowStage(UpdateStatus status)
    {
        CheckNowButton.Content = status.State switch
        {
            UpdateState.Downloading => "Downloading…",
            UpdateState.Preparing => "Installing…",
            _ => CheckingText,
        };
    }

    private void FinishWorking(UpdateStatus status, bool foundByCheck)
    {
        if (status is { State: UpdateState.Ready, Version: { } version })
        {
            ShowUpdateReady();
            if (foundByCheck)
            {
                UpdateFoundByCheck?.Invoke(version);
            }
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

    private void ThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_settings is null)
        {
            return;
        }

        _settings.UseLightTheme = ThemeToggle.IsChecked == true;
        _settings.Save();
        WindowBackdrop.IsLight = _settings.UseLightTheme;
        UpdateText();
        ThemeChanged?.Invoke();
    }

    private async void StartupToggle_Click(object sender, RoutedEventArgs e)
    {
        StartupToggle.IsEnabled = false;
        UpdateText();

        // Shows what actually happened, such as when the updater couldn't undo Task Manager turning it off for everyone.
        StartupToggle.IsChecked = await StartupRegistration.SetEnabledAsync(StartupToggle.IsChecked == true);
        StartupToggle.IsEnabled = true;
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
        ThemeText.Text = ThemeToggle.IsChecked == true ? "Light" : "Dark";
        StartupText.Text = StartupToggle.IsChecked == true ? "On" : "Off";
        AutomaticUpdatesText.Text = AutomaticUpdatesToggle.IsChecked == true ? "On" : "Off";
        TransparencyText.Text = TransparencySlider.Value == 0 ? "Solid" : $"{TransparencySlider.Value:0}%";
    }
}

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using BluetoothAudioManager.App.Models;
using BluetoothAudioManager.Core.Models;
using BluetoothAudioManager.Core.Services;

namespace BluetoothAudioManager.App.Views;

/// <summary>
/// Windows' volume for one device: a mute button, a slider in steps of 2%, and the level. It follows changes made
/// anywhere, such as with the keyboard's volume keys. Windows has no volume for a disconnected device, so the app
/// remembers the last one: the slider still shows it and can change it, and a change made then is set the moment
/// the device connects. A device the app has never seen connected has nothing to show, so its slider is greyed out
/// with no level, and keeps its space so the row's height doesn't change.
/// </summary>
public partial class VolumeSlider : UserControl, IDisposable
{
    private const string VolumeGlyph = "";
    private const string MutedGlyph = "";
    private const string SetOnConnectText = "Set when it connects";
    private const double UnavailableOpacity = 0.4;

    private DeviceVolume? _volume;
    private AppSettings? _settings;
    private string _settingsKey = "";
    private VolumeLevel? _level;
    private bool _hasOutput;
    private bool _showing;

    public VolumeSlider()
    {
        InitializeComponent();
        Show(null);
    }

    private SavedVolume? Saved => _settings?.DeviceVolumes.GetValueOrDefault(_settingsKey);

    /// <param name="deviceName">Names the slider and button for screen readers, such as "AirPods volume".</param>
    /// <param name="settingsKey">The device's key in the settings, where its volume is remembered.</param>
    public void Follow(DeviceVolume volume, string deviceName, AppSettings settings, string settingsKey)
    {
        _volume = volume;
        _settings = settings;
        _settingsKey = settingsKey;
        AutomationProperties.SetName(LevelSlider, $"{deviceName} volume");
        volume.Changed += level => Dispatcher.BeginInvoke(() => ShowFromWindows(level));
        Refresh();
    }

    /// <summary>
    /// Looks for the device's sound output again, as it comes and goes with the connection. When it has just appeared
    /// and the volume was changed while the device was away, that volume is set straight away.
    /// </summary>
    public void Refresh()
    {
        if (_volume is null)
        {
            return;
        }

        var level = _volume.Refresh();
        if (level is not null && Saved is { ApplyOnConnect: true } chosen)
        {
            _volume.SetPercent(chosen.Percent);
            _volume.SetMuted(chosen.IsMuted);
            level = _volume.Current ?? new VolumeLevel(chosen.Percent, chosen.IsMuted);
        }

        ShowFromWindows(level);
    }

    // With an output, Windows' volume is shown and remembered. Without one, the remembered volume is shown.
    private void ShowFromWindows(VolumeLevel? level)
    {
        _hasOutput = level is not null;
        if (level is not null)
        {
            Remember(level, applyOnConnect: false);
        }

        Show(level ?? (Saved is { } saved ? new VolumeLevel(saved.Percent, saved.IsMuted) : null));
    }

    private void Remember(VolumeLevel level, bool applyOnConnect)
    {
        if (_settings is null)
        {
            return;
        }

        if (Saved is { } saved && saved.Percent == level.Percent && saved.IsMuted == level.IsMuted && saved.ApplyOnConnect == applyOnConnect)
        {
            return;
        }

        _settings.DeviceVolumes[_settingsKey] = new SavedVolume
        {
            Percent = level.Percent,
            IsMuted = level.IsMuted,
            ApplyOnConnect = applyOnConnect,
        };
        _settings.Save();
    }

    private void Show(VolumeLevel? level)
    {
        _level = level;
        var muted = level?.IsMuted == true;

        _showing = true;
        IsEnabled = level is not null;
        Root.Opacity = level is null ? UnavailableOpacity : 1;
        LevelSlider.Value = level?.Percent ?? 0;
        LevelSlider.ToolTip = level is not null && !_hasOutput ? SetOnConnectText : null;
        _showing = false;

        MuteButton.Content = muted ? MutedGlyph : VolumeGlyph;
        var action = muted ? "Unmute" : "Mute";
        MuteButton.ToolTip = action;
        AutomationProperties.SetName(MuteButton, action);
        LevelText.Text = level is null ? "" : muted ? "Muted" : $"{level.Percent}%";
    }

    // Connected, the change goes to Windows. Disconnected, it's kept to set when the device connects.
    private void Change(VolumeLevel level)
    {
        if (_hasOutput && _volume is not null)
        {
            _volume.SetPercent(level.Percent);
            _volume.SetMuted(level.IsMuted);
        }

        Remember(level, applyOnConnect: !_hasOutput);
        Show(level);
    }

    private void LevelSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_showing || _level is null)
        {
            return;
        }

        Change(_level with { Percent = (int)LevelSlider.Value });
    }

    // The wheel moves the slider a step, rather than scrolling the window behind it.
    private void LevelSlider_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        LevelSlider.Value += Math.Sign(e.Delta) * LevelSlider.SmallChange;
        e.Handled = true;
    }

    // Muting keeps the level, so unmuting goes back to it.
    private void MuteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_level is not null)
        {
            Change(_level with { IsMuted = !_level.IsMuted });
        }
    }

    public void Dispose() => _volume?.Dispose();
}

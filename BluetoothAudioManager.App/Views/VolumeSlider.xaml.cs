using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using BluetoothAudioManager.Core.Models;
using BluetoothAudioManager.Core.Services;

namespace BluetoothAudioManager.App.Views;

/// <summary>
/// Windows' volume for one device: a mute button, a slider in steps of 2%, and the level. It follows changes made
/// anywhere, such as with the keyboard's volume keys. While the device has no sound output, such as while it's
/// disconnected, it's greyed out with no level, and keeps its space so the row's height doesn't change.
/// </summary>
public partial class VolumeSlider : UserControl, IDisposable
{
    private const string VolumeGlyph = "";
    private const string MutedGlyph = "";
    private const double UnavailableOpacity = 0.4;

    private DeviceVolume? _volume;
    private VolumeLevel? _level;
    private bool _showing;

    public VolumeSlider()
    {
        InitializeComponent();
        Show(null);
    }

    /// <param name="deviceName">Names the slider and button for screen readers, such as "AirPods volume".</param>
    public void Follow(DeviceVolume volume, string deviceName)
    {
        _volume = volume;
        AutomationProperties.SetName(LevelSlider, $"{deviceName} volume");
        volume.Changed += level => Dispatcher.BeginInvoke(() => Show(level));
        Refresh();
    }

    /// <summary>Looks for the device's sound output again, as it comes and goes with the connection.</summary>
    public void Refresh() => Show(_volume?.Refresh());

    private void Show(VolumeLevel? level)
    {
        _level = level;
        var muted = level?.IsMuted == true;

        _showing = true;
        IsEnabled = level is not null;
        Root.Opacity = level is null ? UnavailableOpacity : 1;
        LevelSlider.Value = level?.Percent ?? 0;
        _showing = false;

        MuteButton.Content = muted ? MutedGlyph : VolumeGlyph;
        var action = muted ? "Unmute" : "Mute";
        MuteButton.ToolTip = action;
        AutomationProperties.SetName(MuteButton, action);
        LevelText.Text = level is null ? "" : muted ? "Muted" : $"{level.Percent}%";
    }

    private void LevelSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_showing || _volume is null || _level is null)
        {
            return;
        }

        var percent = (int)LevelSlider.Value;
        _volume.SetPercent(percent);
        Show(_level with { Percent = percent });
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
        if (_volume is null || _level is null)
        {
            return;
        }

        _volume.SetMuted(!_level.IsMuted);
        Show(_level with { IsMuted = !_level.IsMuted });
    }

    public void Dispose() => _volume?.Dispose();
}

using System.Windows.Controls;
using System.Windows.Input;

namespace BluetoothAudioManager.App;

/// <summary>The low battery notification toggle and level slider.</summary>
public partial class LowBatterySetting : UserControl
{
    public LowBatterySetting()
    {
        InitializeComponent();

        // Added here because the slider changes its value while it's being created.
        LevelSlider.ValueChanged += (_, e) => OnChanged();
    }

    /// <summary>Raised with whether alerts are on and the level, when the user changes either.</summary>
    public event Action<bool, int>? Changed;

    public void Show(bool enabled, int level)
    {
        _showing = true;
        Toggle.IsChecked = enabled;
        LevelSlider.Value = level;
        _showing = false;
        UpdateDisplay();
    }

    private bool _showing;

    private void Toggle_Click(object sender, System.Windows.RoutedEventArgs e) => OnChanged();

    private void LevelSlider_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        LevelSlider.Value += Math.Sign(e.Delta) * LevelSlider.TickFrequency;
        e.Handled = true;
    }

    private void OnChanged()
    {
        UpdateDisplay();
        if (!_showing)
        {
            Changed?.Invoke(Toggle.IsChecked == true, (int)LevelSlider.Value);
        }
    }

    private void UpdateDisplay()
    {
        var enabled = Toggle.IsChecked == true;

        StateText.Text = enabled ? "On" : "Off";
        LevelText.Text = $"{(int)LevelSlider.Value}%";
        LevelSlider.IsEnabled = enabled;
        LevelRow.Opacity = enabled ? 1 : 0.5;
    }
}

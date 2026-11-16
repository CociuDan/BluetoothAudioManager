using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using BluetoothAudioManager.App.Models;

namespace BluetoothAudioManager.App.Views;

/// <summary>
/// The device list in edit mode: every paired device in one short list, with a handle to drag it to another place
/// and a switch to show or hide it. Hiding only leaves a device out of this app; it stays paired in Windows.
/// </summary>
public partial class DeviceListEditor : UserControl
{
    private const double LineHeight = 44;

    // The same six dots as the device rows' drag handle.
    private const string DragDots =
        "M0.6,2 A1.4,1.4 0 1 1 3.4,2 A1.4,1.4 0 1 1 0.6,2 Z M4.6,2 A1.4,1.4 0 1 1 7.4,2 A1.4,1.4 0 1 1 4.6,2 Z " +
        "M0.6,7 A1.4,1.4 0 1 1 3.4,7 A1.4,1.4 0 1 1 0.6,7 Z M4.6,7 A1.4,1.4 0 1 1 7.4,7 A1.4,1.4 0 1 1 4.6,7 Z " +
        "M0.6,12 A1.4,1.4 0 1 1 3.4,12 A1.4,1.4 0 1 1 0.6,12 Z M4.6,12 A1.4,1.4 0 1 1 7.4,12 A1.4,1.4 0 1 1 4.6,12 Z";

    private static readonly Brush DraggedBackground = new SolidColorBrush(Color.FromArgb(0x33, 0x80, 0x80, 0x80));

    private readonly List<DeviceListEntry> _entries = [];
    private Border? _draggedLine;

    public DeviceListEditor()
    {
        InitializeComponent();
    }

    /// <summary>The devices in the order they're shown here, each with whether the app shows it.</summary>
    public IReadOnlyList<DeviceListEntry> Entries => _entries;

    /// <summary>Fills the list with the devices, in the order given.</summary>
    public void Show(IEnumerable<DeviceListEntry> entries)
    {
        StopDrag();
        _entries.Clear();
        _entries.AddRange(entries);
        EntriesPanel.Children.Clear();
        _entries.ForEach(entry => EntriesPanel.Children.Add(LineFor(entry)));
    }

    // A divider above, then the handle, the name, Shown or Hidden, and the switch.
    private Border LineFor(DeviceListEntry entry)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var line = new Border { Height = LineHeight, BorderThickness = new Thickness(0, 1, 0, 0), Child = grid, Tag = entry };
        line.SetResourceReference(Border.BorderBrushProperty, "DividerStrokeColorDefaultBrush");

        var dots = new Path
        {
            Data = Geometry.Parse(DragDots),
            Width = 8,
            Height = 14,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };
        dots.SetResourceReference(Shape.FillProperty, "TextFillColorSecondaryBrush");
        var handle = new Border { Background = Brushes.Transparent, Cursor = Cursors.SizeAll, Child = dots, ToolTip = "Drag to reorder" };
        AutomationProperties.SetName(handle, $"Drag {entry.Name} to reorder");
        handle.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            StartDrag(line);
        };
        grid.Children.Add(handle);

        var name = new TextBlock
        {
            Text = entry.Name,
            FontSize = 15,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = entry.Name,
        };
        Grid.SetColumn(name, 1);
        grid.Children.Add(name);

        var state = new TextBlock { FontSize = 12, Margin = new Thickness(12, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        state.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        Grid.SetColumn(state, 2);
        grid.Children.Add(state);

        var toggle = new ToggleButton
        {
            Style = (Style)FindResource("SwitchStyle"),
            IsChecked = entry.Shown,
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(toggle, $"Show {entry.Name}");
        Grid.SetColumn(toggle, 3);
        grid.Children.Add(toggle);

        // A hidden device's name is greyed out, like the switch.
        void ShowState()
        {
            state.Text = entry.Shown ? "Shown" : "Hidden";
            if (entry.Shown)
            {
                name.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
            }
            else
            {
                name.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
            }
        }

        toggle.Click += (_, _) =>
        {
            entry.Shown = toggle.IsChecked == true;
            ShowState();
        };
        ShowState();

        return line;
    }

    private void StartDrag(Border line)
    {
        _draggedLine = line;
        line.Background = DraggedBackground;
        EntriesPanel.CaptureMouse();
    }

    // The dragged line swaps places with the line under the pointer, so the list always shows where it will be.
    private void EntriesPanel_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draggedLine is not { Tag: DeviceListEntry entry } line)
        {
            return;
        }

        var from = EntriesPanel.Children.IndexOf(line);
        var to = Math.Clamp((int)(e.GetPosition(EntriesPanel).Y / LineHeight), 0, _entries.Count - 1);
        if (to == from)
        {
            return;
        }

        EntriesPanel.Children.Remove(line);
        EntriesPanel.Children.Insert(to, line);
        _entries.Remove(entry);
        _entries.Insert(to, entry);
    }

    private void EntriesPanel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => EntriesPanel.ReleaseMouseCapture();

    private void EntriesPanel_LostMouseCapture(object sender, MouseEventArgs e) => StopDrag();

    private void StopDrag()
    {
        if (_draggedLine is { } line)
        {
            line.ClearValue(BackgroundProperty);
        }

        _draggedLine = null;
        if (EntriesPanel.IsMouseCaptured)
        {
            EntriesPanel.ReleaseMouseCapture();
        }
    }
}

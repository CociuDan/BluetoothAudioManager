using System.Windows.Controls;
using System.Windows.Media;

namespace BluetoothAudioManager.App.Views;

/// <summary>
/// The app icon for the title bar. The window behind it is see-through, so the gap around the Bluetooth symbol
/// is really cut out of the speaker, as in the icon file, rather than painted over in the background's color.
/// </summary>
public partial class AppLogo : UserControl
{
    // The shapes and stroke widths of Assets/AppIcon.svg, on its 24 by 24 grid.
    private const string SpeakerBody = "M10.5,15.6 H13.2 L16.8,12.6 V22.6 L13.2,19.6 H10.5 Z";
    private const string InnerWave = "M18.8,15.4 A3.4,3.4 0 0 1 18.8,19.8";
    private const string OuterWave = "M21,13.2 A6.2,6.2 0 0 1 21,22";
    private const string Bluetooth = "M6,7 L18,17 L12,22 V2 L18,7 L6,17";
    private const double BodyOutline = 0.8;
    private const double WaveWidth = 1.5;
    private const double SymbolWidth = 2.6;
    private const double GapWidth = 5.6;

    public AppLogo()
    {
        InitializeComponent();

        // The symbol is drawn smaller and nudged toward the top left, as in the icon file.
        var placement = new TransformGroup();
        placement.Children.Add(new ScaleTransform(0.75, 0.75));
        placement.Children.Add(new TranslateTransform(-2.4, 0.4));

        var body = Geometry.Combine(
            Geometry.Parse(SpeakerBody), Outline(SpeakerBody, BodyOutline), GeometryCombineMode.Union, null);
        var waves = Geometry.Combine(
            Outline(InnerWave, WaveWidth), Outline(OuterWave, WaveWidth), GeometryCombineMode.Union, null);
        var speaker = Geometry.Combine(body, waves, GeometryCombineMode.Union, null);

        var gap = Outline(Bluetooth, GapWidth);
        gap.Transform = placement;
        var symbol = Outline(Bluetooth, SymbolWidth);
        symbol.Transform = placement;

        SpeakerPath.Data = Geometry.Combine(speaker, gap, GeometryCombineMode.Exclude, null);
        SymbolPath.Data = symbol;
    }

    // A line drawn with round ends and corners, as a shape that can be filled and cut.
    private static Geometry Outline(string path, double width)
    {
        var pen = new Pen(Brushes.Black, width)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        };

        return Geometry.Parse(path).GetWidenedPathGeometry(pen);
    }
}

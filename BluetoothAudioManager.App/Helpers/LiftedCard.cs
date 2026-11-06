using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using BluetoothAudioManager.App.Services;

namespace BluetoothAudioManager.App.Helpers;

/// <summary>
/// Makes a device row look lifted off the list while it's dragged: a card in the window's color, 60% solid at the
/// default transparency and following it, with the window's edge and a shadow, drawn above the other rows.
/// </summary>
public static class LiftedCard
{
    // The card reaches this far past the row on the top, right and bottom, so the row's content keeps its place.
    private const double Overhang = 12;

    public static void Apply(Border card, bool lifted)
    {
        Panel.SetZIndex(card, lifted ? 1 : 0);
        // The row's drag handle column already gives room on the left, so the card only reaches out on the other sides.
        // The edge takes 1 pixel of the overhang, so the padding is that much smaller.
        card.Margin = lifted ? new Thickness(-1, -Overhang, -Overhang, -Overhang) : new Thickness(0);
        card.BorderThickness = new Thickness(lifted ? 1 : 0);
        card.Padding = lifted ? new Thickness(0, Overhang - 1, Overhang - 1, Overhang - 1) : new Thickness(0);
        card.CornerRadius = new CornerRadius(lifted ? 8 : 0);
        card.Effect = lifted
            ? new DropShadowEffect { Color = Colors.Black, BlurRadius = 28, ShadowDepth = 10, Direction = 270, Opacity = 0.45 }
            : null;

        if (lifted)
        {
            card.Background = WindowBackdrop.CardBackground;
            card.BorderBrush = WindowBackdrop.Edge;
        }
        else
        {
            card.ClearValue(Border.BackgroundProperty);
            card.ClearValue(Border.BorderBrushProperty);
        }
    }
}

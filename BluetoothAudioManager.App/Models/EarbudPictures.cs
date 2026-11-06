using System.Windows.Media;

namespace BluetoothAudioManager.App.Models;

/// <summary>The pictures an earbuds row shows, like the AirPods row: left bud, right bud and, if it has one, the case.</summary>
public sealed record EarbudPictures(ImageSource Left, ImageSource Right, ImageSource? Case);

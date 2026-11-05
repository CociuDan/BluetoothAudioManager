using System.Windows.Media;
using System.Windows.Media.Imaging;
using BluetoothAudioManager.Core.Enums;
using BluetoothAudioManager.Core.Helpers;

namespace BluetoothAudioManager.App.Helpers;

/// <summary>Pictures of the devices, from the app's Assets folder.</summary>
internal static class ProductPictures
{
    public static ImageSource AirPodsMax => Load("AirPodsMax.png");

    /// <returns>The device's picture, or null if there's none for it.</returns>
    public static ImageSource? ForDeviceName(string deviceName) =>
        DevicePictures.FileFor(deviceName) is { } fileName ? Load(fileName) : null;

    /// <summary>
    /// The pods and case for a family. Families without their own pictures yet, such as AirPods 1 and 2,
    /// and models not recognized yet, such as AirPods 5 and AirPods Pro 3, use the AirPods 4 pictures.
    /// </summary>
    public static (ImageSource Right, ImageSource Left, ImageSource Case) ForAirPods(AirPodsFamily family) => family switch
    {
        AirPodsFamily.AirPods3 => (Load("AirPods3Pod-R.png"), Load("AirPods3Pod-L.png"), Load("AirPods3Case.png")),
        AirPodsFamily.AirPodsPro => (Load("AirPodsProPod-R.png"), Load("AirPodsProPod-L.png"), Load("AirPodsProCase.png")),
        _ => (Load("AirPods4Pod-R.png"), Load("AirPods4Pod-L.png"), Load("AirPods4Case.png")),
    };

    private static BitmapImage Load(string fileName) => new(new Uri($"pack://application:,,,/Assets/{fileName}"));
}

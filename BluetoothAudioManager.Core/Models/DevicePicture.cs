namespace BluetoothAudioManager.Core.Models;

/// <summary>
/// The pictures in the app's Assets folder that show a device: one picture of headphones,
/// or for earbuds a left bud, a right bud and, if it has one, a case.
/// </summary>
public sealed record DevicePicture(string? FileName, string? LeftBud = null, string? RightBud = null, string? Case = null)
{
    public bool IsEarbuds => LeftBud is not null && RightBud is not null;

    public static DevicePicture Headphones(string fileName) => new(fileName);

    /// <summary>Earbuds whose pictures are named after the model: Model-L.png, Model-R.png and Model-case.png.</summary>
    public static DevicePicture Earbuds(string model, bool hasCase = true) =>
        new(null, $"{model}-L.png", $"{model}-R.png", hasCase ? $"{model}-case.png" : null);
}

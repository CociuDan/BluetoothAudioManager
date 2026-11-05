using System.Text.RegularExpressions;

namespace BluetoothAudioManager.Core.Services;

/// <summary>
/// Finds wireless headsets that connect through their own USB adapter. Windows only sees the adapter,
/// as a normal sound output, so the headsets aren't among its paired Bluetooth devices.
/// </summary>
public static partial class UsbHeadsets
{
    /// <returns>The names of the adapters plugged in, such as "Jabra Link 370".</returns>
    public static IReadOnlyList<string> FindAdapterNames() =>
        AudioOutputs.FindActiveNames()
            .Select(AdapterNameFrom)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <param name="outputName">A sound output's name, such as "Speakers (Jabra Link 370)".</param>
    /// <returns>The adapter's name, or null if the output isn't a known headset adapter.</returns>
    public static string? AdapterNameFrom(string outputName) =>
        AdapterPattern().Match(outputName) is { Success: true } match ? match.Groups[1].Value.Trim() : null;

    // Windows adds a number such as "2- " when two outputs share a name.
    [GeneratedRegex(@"\((?:\d+- )?(Jabra Link[^)]*)\)", RegexOptions.IgnoreCase)]
    private static partial Regex AdapterPattern();
}

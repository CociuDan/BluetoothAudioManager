using System.Globalization;
using System.Text.RegularExpressions;
using BluetoothAudioManager.Core.Enums;

namespace BluetoothAudioManager.Core.Helpers;

/// <summary>
/// Recognizes AirPods models from the product ID Windows stores when they're paired.
/// The broadcast messages use the same IDs with their two bytes swapped.
/// </summary>
public static partial class AppleProducts
{
    /// <summary>The model's family, or Unknown for a model not listed here yet, such as AirPods 5 or AirPods Pro 3.</summary>
    public static AirPodsFamily FamilyOf(ushort? productId) => productId switch
    {
        0x2002 or 0x200F => AirPodsFamily.AirPods1And2,
        0x2013 => AirPodsFamily.AirPods3,
        0x2019 or 0x201B => AirPodsFamily.AirPods4,
        0x200E or 0x2014 or 0x2024 => AirPodsFamily.AirPodsPro,
        0x200A or 0x201F => AirPodsFamily.AirPodsMax,
        _ => AirPodsFamily.Unknown,
    };

    /// <param name="deviceInstanceId">A Windows device instance ID, such as one of the Bluetooth services of paired AirPods.</param>
    /// <returns>Apple's product ID, or null if the ID isn't for an Apple device.</returns>
    public static ushort? ParseProductId(string deviceInstanceId) =>
        AppleProductIdPattern().Match(deviceInstanceId) is { Success: true } match
            ? ushort.Parse(match.Groups[1].Value, NumberStyles.HexNumber)
            : null;

    [GeneratedRegex(@"VID&0001004C_PID&([0-9A-F]{4})", RegexOptions.IgnoreCase)]
    private static partial Regex AppleProductIdPattern();
}

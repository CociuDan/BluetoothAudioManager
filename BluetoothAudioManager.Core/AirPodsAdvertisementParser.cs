namespace BluetoothAudioManager.Core;

/// <summary>
/// Parses the proximity pairing message AirPods broadcast over Bluetooth LE.
/// Apple doesn't document this format, so a firmware update could change it.
/// </summary>
public static class AirPodsAdvertisementParser
{
    public const ushort AppleCompanyId = 0x004C;

    private const int MessageLength = 27;
    private const byte ProximityPairingType = 0x07;
    private const byte ProximityPairingLength = 0x19;
    private const byte PairedPrefix = 0x01;
    private const int MaxBatteryValue = 10;

    private static readonly Dictionary<ushort, string> Models = new()
    {
        [0x0220] = "AirPods (1st generation)",
        [0x0F20] = "AirPods (2nd generation)",
        [0x1320] = "AirPods (3rd generation)",
        [0x1920] = "AirPods 4",
        [0x1B20] = "AirPods 4 (ANC)",
        [0x0E20] = "AirPods Pro",
        [0x1420] = "AirPods Pro (2nd generation)",
        [0x2420] = "AirPods Pro (2nd generation, USB-C)",
    };

    // AirPods Max have one battery, which doesn't fit the left, right and case layout.
    private static readonly HashSet<ushort> UnsupportedModels = [0x0A20, 0x1F20];

    /// <param name="data">Apple manufacturer data, without the leading company ID.</param>
    /// <returns>The status, or null if the data isn't a supported AirPods message.</returns>
    public static AirPodsStatus? Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length != MessageLength
            || data[0] != ProximityPairingType
            || data[1] != ProximityPairingLength
            || data[2] != PairedPrefix)
        {
            return null;
        }

        var modelId = (ushort)((data[3] << 8) | data[4]);
        if (UnsupportedModels.Contains(modelId))
        {
            return null;
        }

        // The low nibble and low charging bit belong to the pod sending the message.
        var leftIsBroadcasting = (data[5] & 0x20) != 0;
        var charging = data[7] >> 4;
        var broadcasting = new BatteryReading(ToPercent(data[6] & 0x0F), (charging & 0b0001) != 0);
        var other = new BatteryReading(ToPercent(data[6] >> 4), (charging & 0b0010) != 0);

        return new AirPodsStatus(
            Models.GetValueOrDefault(modelId, $"AirPods (model 0x{modelId:X4})"),
            leftIsBroadcasting ? broadcasting : other,
            leftIsBroadcasting ? other : broadcasting,
            new BatteryReading(ToPercent(data[7] & 0x0F), (charging & 0b0100) != 0));
    }

    // The value 15 means not reporting.
    private static int? ToPercent(int value) => value > MaxBatteryValue ? null : value * 10;
}

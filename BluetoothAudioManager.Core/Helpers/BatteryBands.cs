using BluetoothAudioManager.Core.Enums;

namespace BluetoothAudioManager.Core.Helpers;

public static class BatteryBands
{
    public static BatteryBand FromPercent(int percent) => percent switch
    {
        > 20 => BatteryBand.Good,
        >= 10 => BatteryBand.Warning,
        _ => BatteryBand.Low,
    };
}

namespace BluetoothAudioManager.Core;

public enum BatteryBand
{
    Low,
    Warning,
    Good,
}

public static class BatteryBands
{
    public static BatteryBand FromPercent(int percent) => percent switch
    {
        > 20 => BatteryBand.Good,
        >= 10 => BatteryBand.Warning,
        _ => BatteryBand.Low,
    };
}

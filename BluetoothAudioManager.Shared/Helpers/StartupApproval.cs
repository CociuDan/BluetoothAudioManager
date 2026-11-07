namespace BluetoothAudioManager.Shared.Helpers;

/// <summary>
/// The value Task Manager's Startup apps tab, and Settings' Startup page, keep for a startup entry: 12 bytes, the first
/// even when on and odd when off, and the last 8 the time it was turned off. Windows doesn't document it, but it has
/// worked this way since Windows 8.
/// </summary>
public static class StartupApproval
{
    private const byte OnByte = 2;
    private const byte OffByte = 3;
    private const int Length = 12;
    private const int TimeOffset = 4;

    public static byte[] Value(bool on, DateTime utcNow)
    {
        var value = new byte[Length];
        value[0] = on ? OnByte : OffByte;
        if (!on)
        {
            BitConverter.GetBytes(utcNow.ToFileTimeUtc()).CopyTo(value, TimeOffset);
        }

        return value;
    }

    /// <summary>Whether the value says the entry is turned off. Without a value, Windows treats it as on.</summary>
    public static bool IsOff(object? value) => value is byte[] { Length: > 0 } bytes && (bytes[0] & 1) == 1;
}

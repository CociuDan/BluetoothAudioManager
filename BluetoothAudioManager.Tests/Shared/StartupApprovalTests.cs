using BluetoothAudioManager.Shared.Helpers;

namespace BluetoothAudioManager.Tests.Shared;

public class StartupApprovalTests
{
    [Fact]
    public void Value_On_IsWhatTaskManagerWritesForOn()
    {
        Assert.Equal(new byte[] { 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, StartupApproval.Value(true, DateTime.UtcNow));
    }

    [Fact]
    public void Value_Off_StartsOddAndKeepsTheTime()
    {
        var now = new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);

        var value = StartupApproval.Value(false, now);

        Assert.Equal(12, value.Length);
        Assert.Equal(3, value[0]);
        Assert.Equal(now.ToFileTimeUtc(), BitConverter.ToInt64(value, 4));
    }

    [Fact]
    public void IsOff_ReadsBothItsOwnValuesAndTaskManagers()
    {
        Assert.True(StartupApproval.IsOff(StartupApproval.Value(false, DateTime.UtcNow)));
        Assert.False(StartupApproval.IsOff(StartupApproval.Value(true, DateTime.UtcNow)));
        Assert.True(StartupApproval.IsOff(new byte[] { 3, 0, 0, 0, 0xB3, 0x3E, 0xE1, 0x57, 0xCF, 0x6E, 0xDD, 0x01 }));
        Assert.False(StartupApproval.IsOff(new byte[] { 6, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }));
        Assert.False(StartupApproval.IsOff(null));
        Assert.False(StartupApproval.IsOff(Array.Empty<byte>()));
    }
}

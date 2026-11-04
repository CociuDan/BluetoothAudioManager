using BluetoothAudioManager.Core;

namespace BluetoothAudioManager.Tests;

public class AirPodsAdvertisementParserTests
{
    private static byte[] Message(byte status, byte pods, byte chargingAndCase, ushort model = 0x1420)
    {
        var data = new byte[27];
        data[0] = 0x07;
        data[1] = 0x19;
        data[2] = 0x01;
        data[3] = (byte)(model >> 8);
        data[4] = (byte)model;
        data[5] = status;
        data[6] = pods;
        data[7] = chargingAndCase;
        return data;
    }

    [Fact]
    public void LeftBroadcasting_ReadsLeftFromLowNibble()
    {
        var status = AirPodsAdvertisementParser.Parse(Message(0x20, 0x58, 0x06));

        Assert.NotNull(status);
        Assert.Equal(80, status.Left.Percent);
        Assert.Equal(50, status.Right.Percent);
        Assert.Equal(60, status.Case.Percent);
    }

    [Fact]
    public void RightBroadcasting_ReadsRightFromLowNibble()
    {
        var status = AirPodsAdvertisementParser.Parse(Message(0x00, 0x58, 0x06));

        Assert.NotNull(status);
        Assert.Equal(50, status.Left.Percent);
        Assert.Equal(80, status.Right.Percent);
    }

    [Theory]
    [InlineData(0x20, 0x15, true, false)]
    [InlineData(0x20, 0x25, false, true)]
    [InlineData(0x00, 0x15, false, true)]
    [InlineData(0x00, 0x25, true, false)]
    public void ChargingBits_FollowTheBroadcastingPod(byte statusByte, byte charging, bool left, bool right)
    {
        var status = AirPodsAdvertisementParser.Parse(Message(statusByte, 0x55, charging));

        Assert.NotNull(status);
        Assert.Equal(left, status.Left.IsCharging);
        Assert.Equal(right, status.Right.IsCharging);
        Assert.False(status.Case.IsCharging);
    }

    [Fact]
    public void CaseChargingBit_IsRead()
    {
        var status = AirPodsAdvertisementParser.Parse(Message(0x20, 0x55, 0x45));

        Assert.NotNull(status);
        Assert.True(status.Case.IsCharging);
    }

    [Fact]
    public void BatteryLimits_MapToFullAndNotReporting()
    {
        var status = AirPodsAdvertisementParser.Parse(Message(0x20, 0xFA, 0x0F));

        Assert.NotNull(status);
        Assert.Equal(100, status.Left.Percent);
        Assert.Null(status.Right.Percent);
        Assert.Null(status.Case.Percent);
    }

    [Fact]
    public void KnownModel_IsNamed_AndUnknownModelShowsItsCode()
    {
        Assert.Equal("AirPods Pro", AirPodsAdvertisementParser.Parse(Message(0x20, 0x55, 0x05, 0x0E20))?.Model);
        Assert.Equal("AirPods (model 0xABCD)", AirPodsAdvertisementParser.Parse(Message(0x20, 0x55, 0x05, 0xABCD))?.Model);
    }

    [Fact]
    public void AirPodsMax_IsRejected()
    {
        Assert.Null(AirPodsAdvertisementParser.Parse(Message(0x20, 0x55, 0x05, 0x0A20)));
    }

    [Fact]
    public void OtherAppleMessages_AreRejected()
    {
        var wrongType = Message(0x20, 0x55, 0x05);
        wrongType[0] = 0x10;

        Assert.Null(AirPodsAdvertisementParser.Parse(wrongType));
        Assert.Null(AirPodsAdvertisementParser.Parse(new byte[10]));
    }
}

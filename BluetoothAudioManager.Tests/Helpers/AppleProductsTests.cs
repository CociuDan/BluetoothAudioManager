using BluetoothAudioManager.Core.Enums;
using BluetoothAudioManager.Core.Helpers;

namespace BluetoothAudioManager.Tests.Helpers;

public class AppleProductsTests
{
    [Theory]
    [InlineData(@"BTHENUM\{0000110B-0000-1000-8000-00805F9B34FB}_VID&0001004C_PID&201B\7&1FD99FD9&0&C0B22F6C69C6_C00000000", (ushort)0x201B)]
    [InlineData(@"bthenum\{0000110b-0000-1000-8000-00805f9b34fb}_vid&0001004c_pid&2014\7&1&0&aabbccddeeff_c00000000", (ushort)0x2014)]
    public void ParseProductId_ReadsApplesProductId(string instanceId, ushort expected)
    {
        Assert.Equal(expected, AppleProducts.ParseProductId(instanceId));
    }

    [Theory]
    [InlineData(@"BTHENUM\{0000110B-0000-1000-8000-00805F9B34FB}_VID&0001000A_PID&FFFF\7&2&0&AABBCCDDEEFF_C00000000")]
    [InlineData(@"BTHENUM\DEV_C0B22F6C69C6\7&1FD99FD9&0&BLUETOOTHDEVICE_C0B22F6C69C6")]
    public void ParseProductId_IgnoresOtherDevices(string instanceId)
    {
        Assert.Null(AppleProducts.ParseProductId(instanceId));
    }

    [Theory]
    [InlineData((ushort)0x2002, AirPodsFamily.AirPods1And2)]
    [InlineData((ushort)0x200F, AirPodsFamily.AirPods1And2)]
    [InlineData((ushort)0x2013, AirPodsFamily.AirPods3)]
    [InlineData((ushort)0x2019, AirPodsFamily.AirPods4)]
    [InlineData((ushort)0x201B, AirPodsFamily.AirPods4)]
    [InlineData((ushort)0x200E, AirPodsFamily.AirPodsPro)]
    [InlineData((ushort)0x2014, AirPodsFamily.AirPodsPro)]
    [InlineData((ushort)0x2024, AirPodsFamily.AirPodsPro)]
    [InlineData((ushort)0x200A, AirPodsFamily.AirPodsMax)]
    [InlineData((ushort)0x201F, AirPodsFamily.AirPodsMax)]
    [InlineData((ushort)0x1234, AirPodsFamily.Unknown)]
    public void FamilyOf_GroupsModelsThatLookAlike(ushort productId, AirPodsFamily expected)
    {
        Assert.Equal(expected, AppleProducts.FamilyOf(productId));
    }

    [Fact]
    public void FamilyOf_WithoutProductId_IsUnknown()
    {
        Assert.Equal(AirPodsFamily.Unknown, AppleProducts.FamilyOf(null));
    }
}

using BluetoothAudioManager.Core.Models;
using BluetoothAudioManager.Core.Services;

namespace BluetoothAudioManager.Tests.Services;

public class NearestDeviceSelectorTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void WeakerDevice_DoesNotReplaceStrongerOne()
    {
        var selector = new NearestDeviceSelector(TimeSpan.FromSeconds(10));
        selector.Select(1, -40, Status("mine"), Start);

        var selected = selector.Select(2, -65, Status("neighbour"), Start.AddSeconds(1));

        Assert.Equal("mine", selected.Model);
    }

    [Fact]
    public void StrongerDevice_IsDropped_AfterWindowExpires()
    {
        var selector = new NearestDeviceSelector(TimeSpan.FromSeconds(10));
        selector.Select(1, -40, Status("gone"), Start);

        var selected = selector.Select(2, -65, Status("nearby"), Start.AddSeconds(11));

        Assert.Equal("nearby", selected.Model);
    }

    [Fact]
    public void SameDevice_UsesItsLatestReading()
    {
        var selector = new NearestDeviceSelector(TimeSpan.FromSeconds(10));
        selector.Select(1, -40, Status("old"), Start);

        var selected = selector.Select(1, -50, Status("new"), Start.AddSeconds(1));

        Assert.Equal("new", selected.Model);
    }

    private static AirPodsStatus Status(string model) =>
        new(model, new(50, false), new(50, false), new(50, false));
}

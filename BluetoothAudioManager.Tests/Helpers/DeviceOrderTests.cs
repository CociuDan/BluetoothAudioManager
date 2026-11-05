using BluetoothAudioManager.Core.Helpers;

namespace BluetoothAudioManager.Tests.Helpers;

public class DeviceOrderTests
{
    [Fact]
    public void Sort_FollowsTheSavedOrder()
    {
        var sorted = DeviceOrder.Sort(["AirPods", "MAJOR IV", "Jabra Link 370"], name => name, ["Jabra Link 370", "AirPods", "MAJOR IV"]);

        Assert.Equal(["Jabra Link 370", "AirPods", "MAJOR IV"], sorted);
    }

    [Fact]
    public void Sort_PutsUnsavedDevicesLastInTheirOwnOrder()
    {
        var sorted = DeviceOrder.Sort(["New 1", "AirPods", "New 2", "MAJOR IV"], name => name, ["MAJOR IV", "AirPods"]);

        Assert.Equal(["MAJOR IV", "AirPods", "New 1", "New 2"], sorted);
    }

    [Fact]
    public void Sort_IgnoresCase()
    {
        var sorted = DeviceOrder.Sort(["airpods", "Major IV"], name => name, ["MAJOR IV", "AirPods"]);

        Assert.Equal(["Major IV", "airpods"], sorted);
    }

    [Fact]
    public void Merge_WithEverythingShown_IsTheShownOrder()
    {
        Assert.Equal(["B", "A", "C"], DeviceOrder.Merge(["A", "B", "C"], ["B", "A", "C"]));
    }

    [Fact]
    public void Merge_KeepsAHiddenDeviceAfterTheOneItFollowed()
    {
        // B isn't paired right now; it followed A, so it stays after A.
        Assert.Equal(["C", "A", "B"], DeviceOrder.Merge(["A", "B", "C"], ["C", "A"]));
    }

    [Fact]
    public void Merge_KeepsAHiddenFirstDeviceFirst()
    {
        Assert.Equal(["A", "C", "B"], DeviceOrder.Merge(["A", "B", "C"], ["C", "B"]));
    }

    [Fact]
    public void Merge_WithNothingSaved_IsTheShownOrder()
    {
        Assert.Equal(["B", "A"], DeviceOrder.Merge([], ["B", "A"]));
    }
}

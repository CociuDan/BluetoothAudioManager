using BluetoothAudioManager.Shared.Enums;
using BluetoothAudioManager.Shared.Helpers;
using BluetoothAudioManager.Shared.Models;

namespace BluetoothAudioManager.Tests.Shared;

public class UpdaterPipeTests
{
    [Fact]
    public void Parse_ReadsWhatFormatWrites()
    {
        foreach (var status in new[]
                 {
                     new UpdateStatus(UpdateState.Ready, "0.0.3"),
                     new UpdateStatus(UpdateState.None),
                     new UpdateStatus(UpdateState.Failed),
                 })
        {
            Assert.Equal(status, UpdaterPipe.Parse(UpdaterPipe.Format(status)));
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ready")]
    [InlineData("something else")]
    public void Parse_AnythingElse_IsFailed(string? reply)
    {
        Assert.Equal(new UpdateStatus(UpdateState.Failed), UpdaterPipe.Parse(reply));
    }
}

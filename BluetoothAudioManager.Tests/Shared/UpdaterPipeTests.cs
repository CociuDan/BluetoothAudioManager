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

    [Fact]
    public void ParseReleaseNotes_RoundTripsMultilineMarkdown()
    {
        const string version = "0.0.4";
        const string notes = "## What's changed\n\n- Added a feature\n- Fixed a bug";

        Assert.Equal((version, notes), UpdaterPipe.ParseReleaseNotes(UpdaterPipe.FormatReleaseNotes(version, notes)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unavailable")]
    [InlineData("not base64!")]
    public void ParseReleaseNotes_InvalidReply_IsNull(string? reply)
    {
        Assert.Null(UpdaterPipe.ParseReleaseNotes(reply));
    }
}

using BluetoothAudioManager.Core.Enums;
using BluetoothAudioManager.Core.Services;

namespace BluetoothAudioManager.Tests.Services;

public class EarDetectorTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FirstReading_OnlySetsTheStartingPoint()
    {
        Assert.Equal(EarAction.None, new EarDetector().Update(false, false, Start));
    }

    [Fact]
    public void PodComingOut_Pauses()
    {
        var detector = new EarDetector();
        var now = Start;
        detector.Update(true, true, now);

        Assert.Equal(EarAction.Pause, Hold(detector, true, false, ref now));
    }

    [Fact]
    public void BriefChange_IsIgnored()
    {
        var detector = new EarDetector();
        detector.Update(true, true, Start);

        Assert.Equal(EarAction.None, detector.Update(true, false, Start.AddMilliseconds(100)));
        Assert.Equal(EarAction.None, detector.Update(true, true, Start.AddMilliseconds(200)));
        Assert.Equal(EarAction.None, detector.Update(true, false, Start.AddSeconds(2)));
    }

    [Fact]
    public void PodGoingBack_Resumes()
    {
        var detector = new EarDetector();
        var now = Start;
        detector.Update(true, true, now);
        Hold(detector, true, false, ref now);

        Assert.Equal(EarAction.Resume, Hold(detector, true, true, ref now));
    }

    [Fact]
    public void BothPodsOut_StaysPausedUntilBothAreBack()
    {
        var detector = new EarDetector();
        var now = Start;
        detector.Update(true, true, now);

        Assert.Equal(EarAction.Pause, Hold(detector, true, false, ref now));
        Assert.Equal(EarAction.None, Hold(detector, false, false, ref now));
        Assert.Equal(EarAction.None, Hold(detector, true, false, ref now));
        Assert.Equal(EarAction.Resume, Hold(detector, true, true, ref now));
    }

    [Fact]
    public void UsingOnePod_PausesAndResumes()
    {
        var detector = new EarDetector();
        var now = Start;
        detector.Update(true, false, now);

        Assert.Equal(EarAction.Pause, Hold(detector, false, false, ref now));
        Assert.Equal(EarAction.Resume, Hold(detector, false, true, ref now));
    }

    [Fact]
    public void PuttingPodsIn_WithoutAPause_DoesNotResume()
    {
        var detector = new EarDetector();
        var now = Start;
        detector.Update(false, false, now);

        Assert.Equal(EarAction.None, Hold(detector, true, true, ref now));
    }

    [Fact]
    public void Reset_ForgetsThePause()
    {
        var detector = new EarDetector();
        var now = Start;
        detector.Update(true, true, now);
        Hold(detector, true, false, ref now);

        detector.Reset();
        detector.Update(true, false, now);

        Assert.Equal(EarAction.None, Hold(detector, true, true, ref now));
    }

    /// <summary>Holds a state long enough for the detector to accept it, returning its decision.</summary>
    private static EarAction Hold(EarDetector detector, bool left, bool right, ref DateTimeOffset now)
    {
        detector.Update(left, right, now);
        now = now.AddSeconds(1);
        return detector.Update(left, right, now);
    }
}

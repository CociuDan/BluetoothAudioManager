using BluetoothAudioManager.Core.Enums;

namespace BluetoothAudioManager.Core.Services;

/// <summary>Decides when to pause and resume as pods come out of and go back into the ears.</summary>
public sealed class EarDetector
{
    // A change must hold this long, so a single odd broadcast doesn't interrupt playback.
    private static readonly TimeSpan Stability = TimeSpan.FromMilliseconds(300);

    private int? _confirmedCount;
    private int? _pendingCount;
    private DateTimeOffset _pendingSince;
    private int? _countWhenPaused;

    public EarAction Update(bool leftInEar, bool rightInEar, DateTimeOffset now)
    {
        var count = (leftInEar ? 1 : 0) + (rightInEar ? 1 : 0);

        if (_confirmedCount is not { } confirmed)
        {
            _confirmedCount = count;
            return EarAction.None;
        }

        if (count == confirmed)
        {
            _pendingCount = null;
            return EarAction.None;
        }

        if (count != _pendingCount)
        {
            _pendingCount = count;
            _pendingSince = now;
            return EarAction.None;
        }

        if (now - _pendingSince < Stability)
        {
            return EarAction.None;
        }

        _confirmedCount = count;
        _pendingCount = null;

        if (count < confirmed)
        {
            if (_countWhenPaused is not null)
            {
                return EarAction.None;
            }

            _countWhenPaused = confirmed;
            return EarAction.Pause;
        }

        if (count >= _countWhenPaused)
        {
            _countWhenPaused = null;
            return EarAction.Resume;
        }

        return EarAction.None;
    }

    public void Reset()
    {
        _confirmedCount = null;
        _pendingCount = null;
        _countWhenPaused = null;
    }
}

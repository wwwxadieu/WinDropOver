namespace WinClipboard.Core.Services;

/// <summary>
/// Detects a "shake" — the back-and-forth wiggle that is Dropover's signature way of opening a
/// shelf mid-drag.
///
/// Runs inside the low-level mouse hook callback, where the plan's section 6 risk ("hook chậm
/// khiến Windows tự gỡ hook") applies, so it is deliberately O(1) per sample with no allocation
/// and no history buffer: instead of storing a trail of points and analysing it, it collapses
/// movement into directional segments and counts reversals as they happen.
///
/// A segment is a run of movement in one horizontal direction. Once the cursor has travelled
/// <see cref="MinSegmentDistancePx"/> in some direction, that direction is committed; when the
/// next committed direction is the opposite one, that counts as a reversal. Enough reversals
/// inside <see cref="WindowMs"/> is a shake.
/// </summary>
public sealed class ShakeDetector
{
    /// <summary>Movement below this is treated as jitter and never commits a direction.</summary>
    public int MinSegmentDistancePx { get; init; } = 18;

    /// <summary>Reversals needed to call it a shake. 3 means left-right-left (or right-left-right).</summary>
    public int RequiredDirectionChanges { get; init; } = 3;

    /// <summary>All the reversals must happen within this window, so slow wandering never counts.</summary>
    public int WindowMs { get; init; } = 700;

    private int _segmentStartX;
    private int _committedDirection;   // -1 left, +1 right, 0 = nothing committed yet
    private int _directionChanges;
    private long _firstChangeTimestampMs;
    private bool _hasSegmentStart;

    /// <summary>
    /// Feeds one cursor sample. Returns true exactly once per detected shake; the caller is
    /// expected to act on it and the detector resets itself so one wiggle cannot fire twice.
    /// </summary>
    public bool AddSample(int x, long timestampMs)
    {
        if (!_hasSegmentStart)
        {
            _segmentStartX = x;
            _hasSegmentStart = true;
            return false;
        }

        // Drop reversals that have aged out, so a shake must be a burst rather than accumulated
        // over a long drag.
        if (_directionChanges > 0 && timestampMs - _firstChangeTimestampMs > WindowMs)
        {
            _directionChanges = 0;
        }

        var dx = x - _segmentStartX;
        if (Math.Abs(dx) < MinSegmentDistancePx)
        {
            return false;
        }

        var direction = Math.Sign(dx);
        _segmentStartX = x;

        if (_committedDirection != 0 && direction != _committedDirection)
        {
            if (_directionChanges == 0)
            {
                _firstChangeTimestampMs = timestampMs;
            }
            _directionChanges++;
        }
        _committedDirection = direction;

        if (_directionChanges >= RequiredDirectionChanges)
        {
            Reset();
            return true;
        }

        return false;
    }

    /// <summary>Clears all state. Called when a drag starts or ends, and after a shake fires.</summary>
    public void Reset()
    {
        _hasSegmentStart = false;
        _segmentStartX = 0;
        _committedDirection = 0;
        _directionChanges = 0;
        _firstChangeTimestampMs = 0;
    }
}

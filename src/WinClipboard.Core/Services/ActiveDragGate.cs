namespace WinClipboard.Core.Services;

/// <summary>
/// Holds the shelf triggers shut until a drag-and-drop has actually been observed.
///
/// <see cref="DragThresholdDetector"/> only establishes that the pointer moved with the button
/// held, which every press-and-hold does — dragging a selection rectangle across a folder, or
/// nudging the mouse while gripping it, both look identical to dragging a file. Both used to open
/// the shelf. This gate adds the missing half of the heuristic: once past that threshold, a probe
/// gets a short grace period to confirm a real drag, and if it cannot, the hold is written off
/// entirely so nothing can fire for the rest of it.
///
/// The grace period exists because the two events do not coincide. The source application starts
/// its own drag at the system's threshold, and there is a gap of a few samples before that becomes
/// externally visible; a single check at the threshold would miss most genuine drags. Writing the
/// hold off rather than re-checking forever is what makes a rubber-band selection safe: the user
/// can sweep and wiggle for as long as they like afterwards and no trigger will look at it.
///
/// Pure state machine, fed from inside the mouse hook: no allocation, no clock of its own — the
/// caller passes the hook event's own timestamp.
/// </summary>
public sealed class ActiveDragGate
{
    /// <summary>
    /// How long after the drag threshold a drag may still show up before the hold is written off.
    /// Long enough to cover the source application starting its drag operation, short enough that
    /// a selection sweep is dismissed before the user could shake meaningfully.
    /// </summary>
    public int ConfirmTimeoutMs { get; init; } = 700;

    private bool _thresholdSeen;
    private long _thresholdTimestampMs;

    /// <summary>True once a drag has been confirmed for the current hold; stays true until <see cref="Reset"/>.</summary>
    public bool IsConfirmed { get; private set; }

    /// <summary>True once this hold has been written off as "not a drag"; nothing will confirm it again until <see cref="Reset"/>.</summary>
    public bool HasGivenUp { get; private set; }

    /// <summary>
    /// Feeds one mouse sample. Returns true while the current hold counts as a real drag.
    /// </summary>
    /// <param name="pastDragThreshold">Whether the pointer has already moved far enough from the button-down point to count as a drag attempt.</param>
    /// <param name="dragDetected">What the probe says about this instant. Only consulted after the threshold, so the caller can skip the probe entirely before then.</param>
    /// <param name="timestampMs">The hook event's own timestamp, not the handler's wall clock.</param>
    public bool Update(bool pastDragThreshold, bool dragDetected, long timestampMs)
    {
        if (HasGivenUp)
        {
            return false;
        }

        if (IsConfirmed)
        {
            return true;
        }

        if (!pastDragThreshold)
        {
            return false;
        }

        if (!_thresholdSeen)
        {
            _thresholdSeen = true;
            _thresholdTimestampMs = timestampMs;
        }

        if (dragDetected)
        {
            IsConfirmed = true;
            return true;
        }

        if (timestampMs - _thresholdTimestampMs > ConfirmTimeoutMs)
        {
            HasGivenUp = true;
        }

        return false;
    }

    /// <summary>Clears all state. Called when the button goes down and again when it comes up.</summary>
    public void Reset()
    {
        _thresholdSeen = false;
        _thresholdTimestampMs = 0;
        IsConfirmed = false;
        HasGivenUp = false;
    }
}

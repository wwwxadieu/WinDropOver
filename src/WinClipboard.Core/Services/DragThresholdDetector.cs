namespace WinClipboard.Core.Services;

/// <summary>
/// Pure distance check backing the "is the user dragging" heuristic (plan 3.2): left-button-down
/// on an item followed by movement past a small threshold, without accumulating a point history
/// or analyzing velocity like the discarded mouse-shake approach did.
/// </summary>
public static class DragThresholdDetector
{
    public static bool HasExceededThreshold(int downX, int downY, int currentX, int currentY, int thresholdPx)
    {
        var dx = currentX - downX;
        var dy = currentY - downY;
        return (dx * dx) + (dy * dy) > thresholdPx * thresholdPx;
    }
}

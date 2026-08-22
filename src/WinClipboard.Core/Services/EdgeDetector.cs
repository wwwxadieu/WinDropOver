using WinClipboard.Core.Models;

namespace WinClipboard.Core.Services;

/// <summary>
/// Pure "is the cursor in the hot zone" arithmetic for the Edge Drag Trigger (plan 3.2).
/// The low-level mouse hook callback only forwards coordinates; this is where the actual
/// comparison happens, kept allocation-free and dependency-free so it can run straight out
/// of the hook thread without adding to the callback's cost.
/// </summary>
public static class EdgeDetector
{
    /// <summary>
    /// Returns the nearest enabled edge whose hot zone (the last <paramref name="marginPx"/>
    /// pixels before that edge) contains the point, or null if the point isn't near any
    /// enabled edge. When two edges qualify (a corner), the closer one wins.
    /// </summary>
    public static ScreenEdge? DetectEdge(
        int x,
        int y,
        ScreenRect bounds,
        int marginPx,
        IReadOnlyCollection<ScreenEdge> enabledEdges)
    {
        if (marginPx <= 0 || enabledEdges.Count == 0 || !bounds.Contains(x, y))
        {
            return null;
        }

        ScreenEdge? best = null;
        var bestDistance = int.MaxValue;

        void Consider(ScreenEdge edge, int distance)
        {
            if (distance < 0 || distance > marginPx || !enabledEdges.Contains(edge))
            {
                return;
            }
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = edge;
            }
        }

        Consider(ScreenEdge.Left, x - bounds.Left);
        Consider(ScreenEdge.Right, bounds.Right - x);
        Consider(ScreenEdge.Top, y - bounds.Top);
        Consider(ScreenEdge.Bottom, bounds.Bottom - y);

        return best;
    }
}

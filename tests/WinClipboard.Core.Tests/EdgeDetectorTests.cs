using WinClipboard.Core.Models;
using WinClipboard.Core.Services;
using Xunit;

namespace WinClipboard.Core.Tests;

public class EdgeDetectorTests
{
    private static readonly ScreenRect FullHd = new(Left: 0, Top: 0, Right: 1920, Bottom: 1080);
    private static readonly ScreenEdge[] AllEdges = [ScreenEdge.Left, ScreenEdge.Right, ScreenEdge.Top, ScreenEdge.Bottom];

    [Fact]
    public void PointNearRightEdge_WithinMargin_ReturnsRight()
    {
        var edge = EdgeDetector.DetectEdge(x: 1915, y: 500, FullHd, marginPx: 10, AllEdges);
        Assert.Equal(ScreenEdge.Right, edge);
    }

    [Fact]
    public void PointInMiddleOfScreen_ReturnsNull()
    {
        var edge = EdgeDetector.DetectEdge(x: 960, y: 540, FullHd, marginPx: 10, AllEdges);
        Assert.Null(edge);
    }

    [Fact]
    public void PointNearDisabledEdge_ReturnsNull()
    {
        var edge = EdgeDetector.DetectEdge(x: 1915, y: 500, FullHd, marginPx: 10, [ScreenEdge.Left]);
        Assert.Null(edge);
    }

    [Fact]
    public void PointOutsideBounds_ReturnsNull()
    {
        var edge = EdgeDetector.DetectEdge(x: -5, y: 500, FullHd, marginPx: 10, AllEdges);
        Assert.Null(edge);
    }

    [Fact]
    public void PointInCorner_ReturnsClosestEdge()
    {
        // 3px from the left edge, 8px from the top edge — left should win.
        var edge = EdgeDetector.DetectEdge(x: 3, y: 8, FullHd, marginPx: 10, AllEdges);
        Assert.Equal(ScreenEdge.Left, edge);
    }

    [Fact]
    public void ZeroMargin_NeverTriggers()
    {
        var edge = EdgeDetector.DetectEdge(x: 0, y: 500, FullHd, marginPx: 0, AllEdges);
        Assert.Null(edge);
    }
}

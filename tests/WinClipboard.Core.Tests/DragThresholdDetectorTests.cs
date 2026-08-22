using WinClipboard.Core.Services;
using Xunit;

namespace WinClipboard.Core.Tests;

public class DragThresholdDetectorTests
{
    [Fact]
    public void NoMovement_DoesNotExceedThreshold()
    {
        Assert.False(DragThresholdDetector.HasExceededThreshold(100, 100, 100, 100, thresholdPx: 5));
    }

    [Fact]
    public void SmallMovement_WithinThreshold_DoesNotExceed()
    {
        Assert.False(DragThresholdDetector.HasExceededThreshold(100, 100, 103, 100, thresholdPx: 5));
    }

    [Fact]
    public void MovementBeyondThreshold_Exceeds()
    {
        Assert.True(DragThresholdDetector.HasExceededThreshold(100, 100, 110, 100, thresholdPx: 5));
    }

    [Fact]
    public void DiagonalMovement_UsesEuclideanDistance()
    {
        // 3-4-5 triangle: distance is exactly 5, so a threshold of 5 should not be exceeded yet.
        Assert.False(DragThresholdDetector.HasExceededThreshold(0, 0, 3, 4, thresholdPx: 5));
        Assert.True(DragThresholdDetector.HasExceededThreshold(0, 0, 4, 4, thresholdPx: 5));
    }
}

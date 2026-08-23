using WinClipboard.Core.Services;
using Xunit;

namespace WinClipboard.Core.Tests;

public class ShakeDetectorTests
{
    private static ShakeDetector NewDetector() => new()
    {
        MinSegmentDistancePx = 20,
        RequiredDirectionChanges = 3,
        WindowMs = 700
    };

    /// <summary>Feeds a run of movement in one direction, returning true if a shake fired during it.</summary>
    private static bool Sweep(ShakeDetector detector, ref int x, ref long time, int deltaX, int steps = 3)
    {
        var fired = false;
        for (var i = 0; i < steps; i++)
        {
            x += deltaX;
            time += 30;
            fired |= detector.AddSample(x, time);
        }
        return fired;
    }

    [Fact]
    public void StraightDrag_NeverFires()
    {
        var detector = NewDetector();
        var x = 500;
        long time = 0;

        for (var i = 0; i < 40; i++)
        {
            x += 25;
            time += 20;
            Assert.False(detector.AddSample(x, time));
        }
    }

    [Fact]
    public void ThreeReversals_FiresShake()
    {
        var detector = NewDetector();
        var x = 500;
        long time = 0;

        Assert.False(Sweep(detector, ref x, ref time, +30));  // commits right
        Assert.False(Sweep(detector, ref x, ref time, -30));  // reversal 1
        Assert.False(Sweep(detector, ref x, ref time, +30));  // reversal 2
        Assert.True(Sweep(detector, ref x, ref time, -30));   // reversal 3 -> shake
    }

    [Fact]
    public void Jitter_BelowThreshold_NeverCommitsDirection()
    {
        var detector = NewDetector();
        var x = 500;
        long time = 0;

        // Wobble by less than MinSegmentDistancePx in each direction, many times.
        for (var i = 0; i < 50; i++)
        {
            x += i % 2 == 0 ? 8 : -8;
            time += 20;
            Assert.False(detector.AddSample(x, time));
        }
    }

    [Fact]
    public void ReversalsSpreadBeyondWindow_DoNotAccumulate()
    {
        var detector = NewDetector();
        var x = 500;
        long time = 0;

        Sweep(detector, ref x, ref time, +30);
        Sweep(detector, ref x, ref time, -30);   // reversal 1

        // Long pause pushes the earlier reversal out of the window.
        time += 5_000;

        Sweep(detector, ref x, ref time, +30);   // counted fresh, not as reversal 2
        Assert.False(Sweep(detector, ref x, ref time, -30));
    }

    [Fact]
    public void AfterFiring_ResetsSoOneWiggleFiresOnce()
    {
        var detector = NewDetector();
        var x = 500;
        long time = 0;

        Sweep(detector, ref x, ref time, +30);
        Sweep(detector, ref x, ref time, -30);
        Sweep(detector, ref x, ref time, +30);
        Assert.True(Sweep(detector, ref x, ref time, -30));

        // The very next reversal must not immediately re-fire.
        Assert.False(Sweep(detector, ref x, ref time, +30));
    }

    [Fact]
    public void Reset_ClearsProgress()
    {
        var detector = NewDetector();
        var x = 500;
        long time = 0;

        Sweep(detector, ref x, ref time, +30);
        Sweep(detector, ref x, ref time, -30);
        Sweep(detector, ref x, ref time, +30);

        detector.Reset();

        // Two more reversals would have fired without the reset; after it they must not.
        Sweep(detector, ref x, ref time, -30);
        Assert.False(Sweep(detector, ref x, ref time, +30));
    }

    [Fact]
    public void FasterShake_WithHigherRequirement_StillFires()
    {
        var detector = new ShakeDetector { MinSegmentDistancePx = 20, RequiredDirectionChanges = 5, WindowMs = 700 };
        var x = 500;
        long time = 0;

        Sweep(detector, ref x, ref time, +30);
        for (var i = 0; i < 4; i++)
        {
            Assert.False(Sweep(detector, ref x, ref time, i % 2 == 0 ? -30 : +30));
        }
        Assert.True(Sweep(detector, ref x, ref time, -30));
    }
}

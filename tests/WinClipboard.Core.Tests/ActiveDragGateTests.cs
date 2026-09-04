using WinClipboard.Core.Services;
using Xunit;

namespace WinClipboard.Core.Tests;

public class ActiveDragGateTests
{
    private static ActiveDragGate NewGate() => new() { ConfirmTimeoutMs = 700 };

    [Fact]
    public void BeforeThreshold_NeverConfirms_EvenIfSomethingLooksLikeADrag()
    {
        var gate = NewGate();

        Assert.False(gate.Update(pastDragThreshold: false, dragDetected: true, timestampMs: 0));
        Assert.False(gate.IsConfirmed);
    }

    [Fact]
    public void DragSeenAtThreshold_ConfirmsImmediately()
    {
        var gate = NewGate();

        Assert.True(gate.Update(pastDragThreshold: true, dragDetected: true, timestampMs: 100));
        Assert.True(gate.IsConfirmed);
    }

    [Fact]
    public void DragAppearingShortlyAfterThreshold_StillConfirms()
    {
        var gate = NewGate();

        // The source application takes a few samples to start its drag operation; the gate has to
        // survive that gap or it would reject most genuine drags.
        Assert.False(gate.Update(true, dragDetected: false, timestampMs: 100));
        Assert.False(gate.Update(true, dragDetected: false, timestampMs: 130));
        Assert.True(gate.Update(true, dragDetected: true, timestampMs: 160));
    }

    [Fact]
    public void NoDragWithinTimeout_GivesUpOnTheWholeHold()
    {
        var gate = NewGate();

        // Threshold lands at t=100, so the window closes after t=800.
        for (var t = 100; t <= 900; t += 50)
        {
            Assert.False(gate.Update(true, dragDetected: false, timestampMs: t));
        }

        Assert.True(gate.HasGivenUp);
    }

    /// <summary>
    /// The bug this gate exists for: a rubber-band selection is a long press-and-move that never
    /// becomes a drag. Once written off it must stay written off, even if something later looks
    /// like a drag — otherwise a stray cursor change mid-sweep reopens the hole.
    /// </summary>
    [Fact]
    public void AfterGivingUp_ALaterDragSignalIsIgnoredForTheRestOfTheHold()
    {
        var gate = NewGate();

        for (var t = 100; t <= 900; t += 50)
        {
            gate.Update(true, dragDetected: false, timestampMs: t);
        }
        Assert.True(gate.HasGivenUp);

        Assert.False(gate.Update(true, dragDetected: true, timestampMs: 1000));
        Assert.False(gate.IsConfirmed);
    }

    [Fact]
    public void OnceConfirmed_StaysConfirmedWithoutAskingAgain()
    {
        var gate = NewGate();

        Assert.True(gate.Update(true, dragDetected: true, timestampMs: 100));

        // Mid-drag the cursor passes over a target that restores an ordinary arrow; the drag is
        // still the same drag.
        Assert.True(gate.Update(true, dragDetected: false, timestampMs: 3_000));
        Assert.True(gate.IsConfirmed);
    }

    [Fact]
    public void Reset_ClearsBothOutcomes()
    {
        var gate = NewGate();

        for (var t = 100; t <= 900; t += 50)
        {
            gate.Update(true, dragDetected: false, timestampMs: t);
        }
        Assert.True(gate.HasGivenUp);

        gate.Reset();

        Assert.False(gate.HasGivenUp);
        Assert.False(gate.IsConfirmed);
        Assert.True(gate.Update(true, dragDetected: true, timestampMs: 1_000));
    }

    /// <summary>The timeout is measured from the threshold, not from button-down: a long still hold before moving must not eat the confirmation window.</summary>
    [Fact]
    public void TimeoutIsMeasuredFromTheThresholdSample()
    {
        var gate = NewGate();

        // Button held for five seconds without moving — the gate never saw a threshold sample.
        Assert.False(gate.Update(pastDragThreshold: false, dragDetected: false, timestampMs: 5_000));

        Assert.False(gate.Update(true, dragDetected: false, timestampMs: 5_100));
        Assert.True(gate.Update(true, dragDetected: true, timestampMs: 5_200));
    }
}

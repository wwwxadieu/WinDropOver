namespace WinClipboard.Core.Models;

/// <summary>User-configurable settings for the Edge & Hotkey Drag Trigger (plan 3.2 / 7).</summary>
public sealed class DragTriggerOptions
{
    /// <summary>Edges that open the bubble when the cursor is dragged into their hot zone.</summary>
    public IReadOnlyCollection<ScreenEdge> EnabledEdges { get; set; } = [ScreenEdge.Right];

    /// <summary>Width of the hot zone in pixels, per the plan's "8–12px, configurable" default.</summary>
    public int EdgeMarginPx { get; set; } = 10;

    /// <summary>How far the cursor must move from the mouse-down point before a drag is assumed to be in progress.</summary>
    public int DragThresholdPx { get; set; } = 6;

    /// <summary>
    /// Require an actual drag-and-drop before any trigger may fire, instead of treating every
    /// button-down-and-move as a drag. Off, the shelf opens on a rubber-band selection or an idly
    /// held mouse button; there is no reason to turn it off beyond a machine where the detection
    /// misbehaves, which is why it is a setting at all.
    /// </summary>
    public bool RequireActiveDragAndDrop { get; set; } = true;

    /// <summary>Virtual-key code of the hold-while-dragging hotkey (e.g. VK_RSHIFT). Null disables the hotkey trigger.</summary>
    public int? HoldKeyVirtualCode { get; set; }

    public bool EdgeTriggerEnabled { get; set; } = true;

    public bool HotkeyTriggerEnabled { get; set; } = true;

    /// <summary>Shake-to-open, Dropover's signature gesture.</summary>
    public bool ShakeTriggerEnabled { get; set; } = true;

    /// <summary>How far the cursor must travel in one direction before that direction counts as a shake segment. Lower = more sensitive.</summary>
    public int ShakeSegmentDistancePx { get; set; } = 18;

    /// <summary>Direction reversals required to call it a shake. Higher = harder to trigger accidentally.</summary>
    public int ShakeDirectionChanges { get; set; } = 3;
}

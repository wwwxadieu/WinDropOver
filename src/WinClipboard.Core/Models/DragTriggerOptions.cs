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

    /// <summary>Virtual-key code of the hold-while-dragging hotkey (e.g. VK_RSHIFT). Null disables the hotkey trigger.</summary>
    public int? HoldKeyVirtualCode { get; set; }

    public bool EdgeTriggerEnabled { get; set; } = true;

    public bool HotkeyTriggerEnabled { get; set; } = true;
}

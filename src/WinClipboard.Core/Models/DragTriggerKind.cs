namespace WinClipboard.Core.Models;

/// <summary>Which of the two Dropover-style triggers opened the bubble.</summary>
public enum DragTriggerKind
{
    /// <summary>Cursor entered the hot zone near a configured screen edge while dragging.</summary>
    Edge,

    /// <summary>The configured modifier key was held down while dragging.</summary>
    Hotkey,

    /// <summary>The cursor was shaken back and forth mid-drag — Dropover's signature gesture.</summary>
    Shake
}

namespace WinClipboard.Core.Models;

/// <summary>Persisted user preferences (plan section 2 "Cài đặt" row and section 7 decisions). Serialized as JSON by WinClipboard.Data.SettingsStore.</summary>
public sealed class AppSettings
{
    // --- Shelf trigger (plan 3.2 Edge & Hotkey Drag Trigger) ---
    public List<ScreenEdge> EnabledEdges { get; set; } = [ScreenEdge.Right];
    public int EdgeMarginPx { get; set; } = 10;
    public int DragThresholdPx { get; set; } = 6;

    /// <summary>Only let the shelf open while a drag-and-drop is genuinely under way — see <see cref="DragTriggerOptions.RequireActiveDragAndDrop"/>.</summary>
    public bool RequireActiveDragAndDrop { get; set; } = true;
    public bool EdgeTriggerEnabled { get; set; } = true;
    public bool HotkeyTriggerEnabled { get; set; } = true;
    public ModifierHoldKey HoldKey { get; set; } = ModifierHoldKey.RightShift;

    /// <summary>Shake-to-open — Dropover's signature gesture.</summary>
    public bool ShakeTriggerEnabled { get; set; } = true;
    public int ShakeSegmentDistancePx { get; set; } = 18;
    public int ShakeDirectionChanges { get; set; } = 3;

    // --- Bubble behavior ---
    public bool AutoHideBubbleWhenIdle { get; set; } = true;
    public int AutoHideIdleSeconds { get; set; } = 20;

    // --- Global hotkey to open the clipboard history overlay (Ctrl+Shift+V by default) ---
    public uint HistoryHotkeyModifiers { get; set; } = HotkeyModifierValues.Control | HotkeyModifierValues.Shift;
    public uint HistoryHotkeyVirtualKey { get; set; } = 0x56; // 'V'

    /// <summary>
    /// Brings the shelf back at the pointer (Ctrl+Shift+D by default). Needed because the shelf
    /// hides when you click away from it, and the moment you want it again is when you have
    /// navigated somewhere else to drag its contents out — at which point there is no drag in
    /// progress, so none of the drag gestures can summon it.
    /// </summary>
    public uint ShelfHotkeyModifiers { get; set; } = HotkeyModifierValues.Control | HotkeyModifierValues.Shift;
    public uint ShelfHotkeyVirtualKey { get; set; } = 0x44; // 'D'

    // --- Clipboard history ---
    public int MaxHistoryItems { get; set; } = 500;
    public double? SensitiveDataPurgeAfterHours { get; set; }
    public List<string> ExcludedSourceApps { get; set; } = [];

    // --- Shelf persistence default (plan 7: off by default, opt-in per user decision TBD) ---
    public bool NewShelvesPersistByDefault { get; set; }

    // --- System ---
    public bool StartWithWindows { get; set; } = true;
}

/// <summary>RegisterHotKey modifier flag values, duplicated here (rather than referencing WinClipboard.Interop) so Core stays platform-neutral while still using the exact bit values Win32 expects.</summary>
public static class HotkeyModifierValues
{
    public const uint Alt = 0x0001;
    public const uint Control = 0x0002;
    public const uint Shift = 0x0004;
    public const uint Win = 0x0008;
}

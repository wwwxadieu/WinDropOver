namespace WinClipboard.Core.Models;

/// <summary>Candidate keys for the "hold while dragging" bubble trigger. Right-side modifiers are offered first since they're comfortable to hold with the mouse hand's opposite hand while the other hand drives the drag.</summary>
public enum ModifierHoldKey
{
    None,
    RightShift,
    LeftShift,
    RightControl,
    LeftControl,
    RightAlt,
    LeftAlt
}

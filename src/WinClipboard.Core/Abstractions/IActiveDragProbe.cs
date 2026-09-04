namespace WinClipboard.Core.Abstractions;

/// <summary>
/// Answers the one question a low-level mouse hook cannot answer for itself: is the left button
/// being held because a drag-and-drop is actually under way, or for some entirely different
/// reason?
///
/// The hook sees "button down, cursor moved" and nothing else, and that is equally true of
/// dragging files out of Explorer and of rubber-band selecting a dozen of them, of dragging a
/// scrollbar, of selecting text, or of a hand resting on the button while the mouse drifts. A
/// trigger built on the hook alone therefore fires on all of them, which is exactly the bug this
/// interface exists to close.
///
/// Implemented against Win32 in WinClipboard.Interop; abstracted here so the gating logic can be
/// tested without a real drag on a real desktop.
/// </summary>
public interface IActiveDragProbe
{
    /// <summary>
    /// Called on left-button-down, before any drag could have started, so the implementation can
    /// capture whatever "not dragging yet" looks like on this machine right now.
    /// </summary>
    void OnPointerPressed();

    /// <summary>
    /// True when a drag-and-drop operation appears to be running at this instant. Called from
    /// inside the mouse hook callback, so implementations must be cheap and allocation-free.
    /// </summary>
    bool IsDragInProgress();
}

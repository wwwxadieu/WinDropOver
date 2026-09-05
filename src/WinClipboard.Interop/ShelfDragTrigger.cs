using WinClipboard.Core.Abstractions;
using WinClipboard.Core.Models;
using WinClipboard.Core.Services;
using WinClipboard.Interop.Hooks;

namespace WinClipboard.Interop;

public sealed class DragTriggerEventArgs : EventArgs
{
    public required DragTriggerKind Kind { get; init; }
    public required ScreenEdge? Edge { get; init; }
    public required int X { get; init; }
    public required int Y { get; init; }
}

/// <summary>
/// Decides when a drag in progress should raise the shelf bubble. Combines the left-button-down +
/// move-past-threshold "is dragging" heuristic with any of three triggers, each independently
/// switchable: a held hotkey, proximity to a screen edge, or a shake.
///
/// All three exist because Dropover itself offers several (its App Store listing calls the shake
/// its "signature activation method", alongside a keyboard shortcut and the menu bar). The
/// project's plan document had asserted the opposite — that shake was an invention with no
/// precedent and that Dropover used edge-drag — so edge-drag was built first and shake added
/// later; both are kept and the user picks.
///
/// None of the three may fire until a drag-and-drop has actually been observed. Button-down plus
/// movement past a threshold, which is all the hook can see, describes a rubber-band selection or
/// an idly gripped mouse just as well as it describes dragging a file - and while that was the
/// whole test, both of those opened the shelf. <see cref="ActiveDragGate"/> and
/// <see cref="IActiveDragProbe"/> supply the missing half.
///
/// Runs entirely off events from the two low-level hooks, so it must be wired up on the same
/// background thread that owns <see cref="Win32MessageWindow"/>.
/// </summary>
public sealed class ShelfDragTrigger
{
    private readonly DragTriggerOptions _options;
    private readonly Func<ScreenRect> _getVirtualScreenBounds;
    private readonly ShakeDetector _shakeDetector;
    private readonly ActiveDragGate _dragGate = new();
    private readonly IActiveDragProbe _dragProbe;

    private bool _leftButtonDown;
    private bool _isDragging;
    private bool _alreadyTriggeredThisDrag;
    private bool _holdKeyDown;
    private int _downX;
    private int _downY;

    public event EventHandler<DragTriggerEventArgs>? Triggered;

    /// <param name="dragProbe">How to tell a real drag from any other press-and-hold. Defaults to the cursor-based probe; injectable so tests need not run one.</param>
    public ShelfDragTrigger(
        DragTriggerOptions options,
        Func<ScreenRect> getVirtualScreenBounds,
        LowLevelMouseHook mouseHook,
        LowLevelKeyboardHook keyboardHook,
        IActiveDragProbe? dragProbe = null)
    {
        _options = options;
        _getVirtualScreenBounds = getVirtualScreenBounds;
        _dragProbe = dragProbe ?? new ShellDragProbe();
        _shakeDetector = new ShakeDetector
        {
            MinSegmentDistancePx = options.ShakeSegmentDistancePx,
            RequiredDirectionChanges = options.ShakeDirectionChanges
        };
        mouseHook.MouseEvent += OnMouseEvent;
        keyboardHook.KeyEvent += OnKeyEvent;
    }

    private void OnKeyEvent(object? sender, KeyboardHookEventArgs e)
    {
        if (_options.HoldKeyVirtualCode is int vk && e.VirtualKeyCode == vk)
        {
            _holdKeyDown = e.IsKeyDown;
        }
    }

    private void OnMouseEvent(object? sender, MouseHookEventArgs e)
    {
        if (e.IsLeftButtonDown)
        {
            _leftButtonDown = true;
            _isDragging = false;
            _alreadyTriggeredThisDrag = false;
            _downX = e.X;
            _downY = e.Y;
            _shakeDetector.Reset();
            _dragGate.Reset();
            // Has to happen now, while the pointer is definitely not dragging anything, because
            // that is the baseline the probe compares every later sample against.
            _dragProbe.OnPointerPressed();
            return;
        }

        if (e.IsLeftButtonUp)
        {
            _leftButtonDown = false;
            _isDragging = false;
            _alreadyTriggeredThisDrag = false;
            _shakeDetector.Reset();
            _dragGate.Reset();
            return;
        }

        if (!_leftButtonDown || _alreadyTriggeredThisDrag)
        {
            return;
        }

        if (!_isDragging)
        {
            _isDragging = DragThresholdDetector.HasExceededThreshold(_downX, _downY, e.X, e.Y, _options.DragThresholdPx);
            if (!_isDragging)
            {
                return;
            }
        }

        // Nothing below may fire on a hold that is not carrying anything. The probe is only asked
        // until the gate settles one way or the other - once it has, this costs a bool.
        if (_options.RequireActiveDragAndDrop && !_dragGate.IsConfirmed)
        {
            var confirmedNow = !_dragGate.HasGivenUp
                && _dragGate.Update(pastDragThreshold: true, _dragProbe.IsDragInProgress(), e.TimestampMs);

            // Whatever the pointer did before the drag existed - a selection sweep, a wobble while
            // the hand settled - must not be banked towards a shake that fires the instant it does.
            _shakeDetector.Reset();

            if (!confirmedNow)
            {
                return;
            }
        }

        if (_options.HotkeyTriggerEnabled && _holdKeyDown)
        {
            Fire(DragTriggerKind.Hotkey, edge: null, e.X, e.Y);
            return;
        }

        // Fed before the edge check so a shake anywhere on screen wins; the detector keeps its own
        // state, so it must see every sample even when the other triggers are the ones that fire.
        if (_options.ShakeTriggerEnabled && _shakeDetector.AddSample(e.X, e.TimestampMs))
        {
            Fire(DragTriggerKind.Shake, edge: null, e.X, e.Y);
            return;
        }

        if (_options.EdgeTriggerEnabled)
        {
            var edge = EdgeDetector.DetectEdge(e.X, e.Y, _getVirtualScreenBounds(), _options.EdgeMarginPx, _options.EnabledEdges);
            if (edge is not null)
            {
                Fire(DragTriggerKind.Edge, edge, e.X, e.Y);
            }
        }
    }

    private void Fire(DragTriggerKind kind, ScreenEdge? edge, int x, int y)
    {
        _alreadyTriggeredThisDrag = true;
        Triggered?.Invoke(this, new DragTriggerEventArgs { Kind = kind, Edge = edge, X = x, Y = y });
    }
}

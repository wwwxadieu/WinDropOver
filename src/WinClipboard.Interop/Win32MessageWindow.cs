using System.Runtime.InteropServices;
using WinClipboard.Interop.Hooks;
using WinClipboard.Interop.Native;

namespace WinClipboard.Interop;

/// <summary>
/// The single background Win32 message-loop thread described in plan section 3.3: it owns the
/// mouse hook, keyboard hook, registered hotkeys and the clipboard format listener, all of which
/// must live on one thread with a running GetMessage loop. Kept isolated from the WPF UI thread
/// so a hang or crash here cannot freeze the UI, and vice versa.
/// </summary>
public sealed class Win32MessageWindow : IDisposable
{
    private const string ClassName = "WinClipboard.MessageWindow";

    // How often to check the mouse hook is still alive, and how long it may stay quiet first.
    // The gap is generous on purpose: someone can genuinely not touch their mouse for a while,
    // and reinstalling a healthy hook costs two syscalls, so erring towards reinstalling is
    // cheap while erring the other way leaves the app silently dead until it is restarted.
    private const uint WatchdogIntervalMs = 5_000;
    private const long HookQuietBeforeReinstallMs = 20_000;
    private static readonly IntPtr WatchdogTimerId = new(1);

    private readonly WndProc _wndProc;
    private readonly List<(int Id, uint Modifiers, uint VirtualKey)> _pendingHotkeys = [];
    private Thread? _thread;
    private IntPtr _hwnd;
    private uint _threadId;
    private readonly ManualResetEventSlim _ready = new(false);
    private volatile bool _stopRequested;

    public LowLevelMouseHook MouseHook { get; } = new();
    public LowLevelKeyboardHook KeyboardHook { get; } = new();

    /// <summary>Raised on the background thread when a registered hotkey fires (WM_HOTKEY).</summary>
    public event EventHandler<int>? HotkeyPressed;

    /// <summary>Raised on the background thread when the system clipboard changes (WM_CLIPBOARDUPDATE).</summary>
    public event EventHandler? ClipboardChanged;

    public Win32MessageWindow()
    {
        _wndProc = WndProcCallback;
        MouseHook.CallbackFailed += (_, ex) => CallbackFailed?.Invoke(this, ex);
    }

    /// <summary>Queue a hotkey to register once the message loop starts. id must be unique per window.</summary>
    public void RegisterHotkey(int id, uint modifiers, uint virtualKey) =>
        _pendingHotkeys.Add((id, modifiers | NativeConstants.MOD_NOREPEAT, virtualKey));

    public void Start()
    {
        if (_thread is not null)
        {
            return;
        }
        _thread = new Thread(RunMessageLoop)
        {
            IsBackground = true,
            Name = "WinClipboard-Win32MessageLoop"
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        _ready.Wait();
    }

    public void Stop()
    {
        if (_thread is null)
        {
            return;
        }
        _stopRequested = true;
        if (_hwnd != IntPtr.Zero)
        {
            NativeMethods.PostMessage(_hwnd, NativeConstants.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }
        _thread.Join(TimeSpan.FromSeconds(2));
        _thread = null;
    }

    private void RunMessageLoop()
    {
        var wndClass = new WNDCLASSEX
        {
            cbSize = Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = NativeMethods.GetModuleHandle(null),
            lpszClassName = ClassName
        };
        NativeMethods.RegisterClassEx(ref wndClass);

        _hwnd = NativeMethods.CreateWindowEx(
            0, ClassName, ClassName, 0,
            0, 0, 0, 0,
            IntPtr.Zero, IntPtr.Zero, wndClass.hInstance, IntPtr.Zero);

        _threadId = (uint)Environment.CurrentManagedThreadId;

        MouseHook.Install();
        KeyboardHook.Install();
        foreach (var (id, modifiers, vk) in _pendingHotkeys)
        {
            NativeMethods.RegisterHotKey(_hwnd, id, modifiers, vk);
        }
        NativeMethods.AddClipboardFormatListener(_hwnd);
        NativeMethods.SetTimer(_hwnd, WatchdogTimerId, WatchdogIntervalMs, IntPtr.Zero);

        _ready.Set();

        while (!_stopRequested && NativeMethods.GetMessage(out var msg, IntPtr.Zero, 0, 0))
        {
            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessage(ref msg);
        }

        Cleanup();
    }

    private void Cleanup()
    {
        NativeMethods.KillTimer(_hwnd, WatchdogTimerId);
        NativeMethods.RemoveClipboardFormatListener(_hwnd);
        foreach (var (id, _, _) in _pendingHotkeys)
        {
            NativeMethods.UnregisterHotKey(_hwnd, id);
        }
        KeyboardHook.Uninstall();
        MouseHook.Uninstall();
        if (_hwnd != IntPtr.Zero)
        {
            NativeMethods.DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }
    }

    /// <summary>Raised when handling a window message threw. Reported rather than rethrown, because an exception unwinding out of a WndProc kills the process.</summary>
    public event EventHandler<Exception>? CallbackFailed;

    private IntPtr WndProcCallback(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            return DispatchMessage(hWnd, msg, wParam, lParam);
        }
        catch (Exception ex)
        {
            // Same reasoning as the hook callback: this frame is called from native code, so an
            // escaping exception is fatal rather than catchable. Report and carry on.
            CallbackFailed?.Invoke(this, ex);
            return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
        }
    }

    private IntPtr DispatchMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case NativeConstants.WM_HOTKEY:
                HotkeyPressed?.Invoke(this, wParam.ToInt32());
                return IntPtr.Zero;

            case NativeConstants.WM_CLIPBOARDUPDATE:
                ClipboardChanged?.Invoke(this, EventArgs.Empty);
                return IntPtr.Zero;

            case NativeConstants.WM_TIMER when wParam == WatchdogTimerId:
                CheckHooksAlive();
                return IntPtr.Zero;

            case NativeConstants.WM_CLOSE:
                NativeMethods.PostQuitMessage(0);
                return IntPtr.Zero;

            default:
                return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
        }
    }

    /// <summary>
    /// Windows silently removes a low-level hook whose callback overruns LowLevelHooksTimeout
    /// (300ms by default), gives no notification that it did, and offers no way to ask whether a
    /// hook is still installed. The callbacks simply stop, and every trigger stops working until
    /// the application is restarted.
    ///
    /// Care in the callbacks is the real defence, but it cannot be a complete one: a GC pause or
    /// a loaded machine can overrun the limit through no fault of this code. So notice the
    /// silence and put the hook back. Runs on WM_TIMER, which means it runs on the thread that
    /// owns the hooks — the only thread from which they may be reinstalled.
    /// </summary>
    private void CheckHooksAlive()
    {
        var quietFor = Environment.TickCount64 - MouseHook.LastCallbackTicks;
        if (quietFor > HookQuietBeforeReinstallMs)
        {
            MouseHook.Reinstall();
            KeyboardHook.Reinstall();
        }
    }

    public void Dispose()
    {
        Stop();
        _ready.Dispose();
    }
}

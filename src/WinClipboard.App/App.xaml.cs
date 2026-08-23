using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using WinClipboard.App.Services;
using WinClipboard.App.Views;
using WinClipboard.Core.Models;
using WinClipboard.Core.Services;
using WinClipboard.Data;
using WinClipboard.Interop;

namespace WinClipboard.App;

public partial class App : System.Windows.Application
{
    private const int HistoryHotkeyId = 1;
    private const int ShelfHotkeyId = 2;

    private Mutex? _singleInstanceMutex;
    private Win32MessageWindow? _win32Window;
    private ShelfDragTrigger? _dragTrigger;
    private ClipboardMonitorService? _clipboardMonitor;
    private ShelfSessionManager? _shelfSession;
    private TrayIconService? _tray;

    private HistoryOverlayWindow? _historyWindow;
    private BubbleWindow? _bubbleWindow;
    private ShelfPanelWindow? _panelWindow;
    private SettingsWindow? _settingsWindow;

    public AppSettings Settings { get; private set; } = new();
    public SettingsStore SettingsPersistence { get; private set; } = null!;
    public ClipboardRepository ClipboardRepository { get; private set; } = null!;
    public ShelfSessionManager ShelfSession => _shelfSession!;
    public QuickActionsEngine QuickActions { get; private set; } = null!;
    public PasteService PasteService { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Screenshot mode renders each window to a PNG and exits. It deliberately skips the
        // single-instance mutex, the Win32 hooks and the tray icon so it can run unattended on
        // a CI runner alongside (or instead of) a real instance.
        if (ScreenshotCapture.TryParseOutputDirectory(e.Args, out var screenshotDirectory))
        {
            BuildDataServices(
                databasePath: Path.Combine(screenshotDirectory, "screenshot-data", "winclipboard.db"),
                settingsPath: Path.Combine(screenshotDirectory, "screenshot-data", "settings.json"),
                thumbnailDirectory: Path.Combine(screenshotDirectory, "screenshot-data", "thumbnails"));
            _ = RunScreenshotModeAsync(screenshotDirectory);
            return;
        }

        // Record why the app died, wherever it dies. Until now a crash on a user's machine left
        // nothing behind at all — no console, no log — so diagnosing one meant reading the source
        // and guessing.
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            CrashLog.Write("AppDomain.UnhandledException", args.ExceptionObject as Exception);

        // Logged AND swallowed. Until now this only logged, which left the default behaviour
        // intact: WPF tears the process down after an unhandled exception on the UI thread. For
        // an app that lives in the tray all day and is driven by drag gestures, that means one
        // bad file — an image the decoder chokes on, a path the shell reports oddly — takes the
        // whole app with it and loses the shelf. Nothing here holds unmanaged state that a failed
        // handler could corrupt: the database work is transactional, and the shelf is rebuilt
        // from it on the next reload. Continuing costs a failed action; dying costs the session.
        DispatcherUnhandledException += (_, args) =>
        {
            CrashLog.Write("Dispatcher.UnhandledException", args.Exception);
            ReportRecoveredFailure(args.Exception);
            args.Handled = true;
        };

        // Same reasoning: an exception in a task nobody awaited used to be able to reach the
        // finalizer thread and kill the process.
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            CrashLog.Write("TaskScheduler.UnobservedTaskException", args.Exception);
            args.SetObserved();
        };

        _singleInstanceMutex = new Mutex(initiallyOwned: true, "WinClipboard.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            System.Windows.MessageBox.Show("WinClipboard đang chạy rồi (xem system tray).", "WinClipboard");
            Shutdown();
            return;
        }

        BuildDataServices(
            databasePath: SqliteConnectionFactory.DefaultDatabasePath(),
            settingsPath: WinClipboard.Data.SettingsStore.DefaultSettingsPath(),
            thumbnailDirectory: Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WinClipboard", "thumbnails"));

        _win32Window = new Win32MessageWindow();
        _win32Window.RegisterHotkey(HistoryHotkeyId, Settings.HistoryHotkeyModifiers, Settings.HistoryHotkeyVirtualKey);
        _win32Window.RegisterHotkey(ShelfHotkeyId, Settings.ShelfHotkeyModifiers, Settings.ShelfHotkeyVirtualKey);
        _win32Window.HotkeyPressed += OnHotkeyPressed;
        _win32Window.ClipboardChanged += OnClipboardChangedOnBackgroundThread;
        // These are failures the interop layer swallowed to keep a native callback from killing
        // the process. Swallowed is not the same as fine, so they still have to be recorded.
        _win32Window.CallbackFailed += (_, ex) => CrashLog.Write("Win32 callback", ex);

        _dragTrigger = new ShelfDragTrigger(
            BuildDragTriggerOptions(Settings),
            VirtualScreen.GetBounds,
            _win32Window.MouseHook,
            _win32Window.KeyboardHook);
        _dragTrigger.Triggered += OnDragTriggered;

        _win32Window.Start();

        StartupRegistration.SetEnabled(Settings.StartWithWindows);

        _tray = new TrayIconService();
        _tray.OpenShelfRequested += (_, _) => Dispatcher.Invoke(ShowShelfFromTray);
        _tray.OpenHistoryRequested += (_, _) => Dispatcher.Invoke(ToggleHistoryWindow);
        _tray.OpenSettingsRequested += (_, _) => Dispatcher.Invoke(ShowSettingsWindow);
        _tray.ExitRequested += (_, _) => Dispatcher.Invoke(Shutdown);

        // Build the bubble now rather than on the first trigger. Two reasons, both about the
        // moment it is summoned: a window only becomes an OLE drop target once its handle exists
        // and it has been registered, and doing that mid-drag leaves the registration racing the
        // drop; and parsing its XAML for the first time is exactly the kind of delay that used to
        // happen inside the mouse hook. Neither belongs on the critical path of a gesture.
        _bubbleWindow = new BubbleWindow(this);
        new WindowInteropHelper(_bubbleWindow).EnsureHandle();

        _ = _shelfSession.EnsureDefaultShelfAsync();
    }

    /// <summary>
    /// Wires up everything that does not touch the OS: settings, SQLite, the clipboard monitor
    /// and the Quick Actions engine. Shared by normal startup and screenshot mode, which differ
    /// only in where the data lives and in whether hooks/tray are started afterwards.
    /// </summary>
    private void BuildDataServices(string databasePath, string settingsPath, string thumbnailDirectory)
    {
        SettingsPersistence = new SettingsStore(settingsPath);
        Settings = SettingsPersistence.Load();

        var connectionFactory = new SqliteConnectionFactory(databasePath);
        ClipboardRepository = new ClipboardRepository(connectionFactory);
        var shelfRepository = new ShelfRepository(connectionFactory);
        _shelfSession = new ShelfSessionManager(shelfRepository);

        _clipboardMonitor = new ClipboardMonitorService(ClipboardRepository, thumbnailDirectory);
        PasteService = new PasteService(_clipboardMonitor);

        var clipboardWriter = new ClipboardWriterImpl(Dispatcher, _clipboardMonitor);
        var shellLauncher = new ShellLauncherImpl(Dispatcher, () => _panelWindow is not null
            ? new WindowInteropHelper(_panelWindow).Handle
            : IntPtr.Zero);
        QuickActions = new QuickActionsEngine(_shelfSession, clipboardWriter, shellLauncher);
    }

    private async Task RunScreenshotModeAsync(string outputDirectory)
    {
        Shutdown(await ScreenshotCapture.RunAsync(this, outputDirectory));
    }

    private static DragTriggerOptions BuildDragTriggerOptions(AppSettings settings) => new()
    {
        EnabledEdges = settings.EnabledEdges,
        EdgeMarginPx = settings.EdgeMarginPx,
        DragThresholdPx = settings.DragThresholdPx,
        EdgeTriggerEnabled = settings.EdgeTriggerEnabled,
        HotkeyTriggerEnabled = settings.HotkeyTriggerEnabled,
        HoldKeyVirtualCode = ModifierHoldKeyMap.ToVirtualKeyCode(settings.HoldKey),
        ShakeTriggerEnabled = settings.ShakeTriggerEnabled,
        ShakeSegmentDistancePx = settings.ShakeSegmentDistancePx,
        ShakeDirectionChanges = settings.ShakeDirectionChanges
    };

    private void OnHotkeyPressed(object? sender, int hotkeyId)
    {
        if (hotkeyId == HistoryHotkeyId)
        {
            Dispatcher.InvokeAsync(ToggleHistoryWindow);
        }
        else if (hotkeyId == ShelfHotkeyId)
        {
            // At the pointer, because the point of this shortcut is to put the shelf where you
            // have just navigated to in order to drag its contents out.
            Dispatcher.InvokeAsync(() => _ = ReportIfFaultedAsync(_bubbleWindow!.ShowAtCursorAsync()));
        }
    }

    private void OnClipboardChangedOnBackgroundThread(object? sender, EventArgs e)
    {
        // WM_CLIPBOARDUPDATE arrives on the Win32 message-loop thread; Clipboard reads must
        // happen on the WPF UI thread (see ClipboardMonitorService's class remarks). Reading the
        // clipboard can block for a long time on its own - another application may hold it open,
        // and a large bitmap takes real work to marshal - so this must not be waited on here.
        Dispatcher.InvokeAsync(() => _clipboardMonitor!.OnClipboardChanged());
    }

    private void OnDragTriggered(object? sender, DragTriggerEventArgs e)
    {
        Dispatcher.InvokeAsync(() => ShowBubble(e));
    }

    private void ToggleHistoryWindow()
    {
        _historyWindow ??= new HistoryOverlayWindow(this);
        if (_historyWindow.IsVisible)
        {
            _historyWindow.HideOverlay();
        }
        else
        {
            // Fire-and-forget from a UI event: a failure here should log, not take the app down.
            _ = ReportIfFaultedAsync(_historyWindow.ShowOverlayAsync());
        }
    }

    private void ShowBubble(DragTriggerEventArgs trigger)
    {
        _bubbleWindow ??= new BubbleWindow(this);
        // The trigger knows where the drag was when it fired, and that is where the shelf belongs.
        // This used to pass only the edge and throw the coordinates away, which parked the shelf
        // against the side of the screen no matter where the user actually was.
        _ = ReportIfFaultedAsync(_bubbleWindow.ShowAtPointAsync(trigger.X, trigger.Y));
    }

    /// <summary>
    /// Opens the bubble and its panel from the tray. Unlike the drag triggers this happens with no
    /// drag in progress, so it goes straight to the expanded panel — the point is to look at what
    /// the shelf holds, not to catch an incoming drop.
    /// </summary>
    private void ShowShelfFromTray()
    {
        _bubbleWindow ??= new BubbleWindow(this);
        _ = ReportIfFaultedAsync(ShowShelfAsync());
    }

    private async Task ShowShelfAsync()
    {
        // No drag to take a position from, so use wherever the pointer is — which, having just
        // come from the tray menu, is close to where the user is looking.
        await _bubbleWindow!.ShowAtCursorAsync();
        await GetOrCreatePanelWindow().ShowNextToAsync(_bubbleWindow, Settings.EnabledEdges.FirstOrDefault(ScreenEdge.Right));
    }

    /// <summary>
    /// Awaits a fire-and-forget task and surfaces a failure instead of letting it vanish into an
    /// unobserved Task. Screenshot mode routes this to its log; a normal run shows a message box.
    /// </summary>
    private DateTimeOffset _lastFailureNotice = DateTimeOffset.MinValue;

    /// <summary>
    /// Tells the user an action failed, at most once a minute. Rate limited because the failures
    /// worth surfacing tend to repeat — the same file dropped again, a reload that throws on every
    /// tick — and a tray balloon per occurrence would be worse than the silence it replaces.
    /// </summary>
    private void ReportRecoveredFailure(Exception exception)
    {
        ScreenshotCapture.ReportBackgroundFailure(exception);

        var now = DateTimeOffset.UtcNow;
        if (_tray is null || now - _lastFailureNotice < TimeSpan.FromMinutes(1))
        {
            return;
        }
        _lastFailureNotice = now;
        _tray.ShowWarning(
            "WinClipboard gặp lỗi",
            $"Một thao tác vừa thất bại nhưng ứng dụng vẫn chạy. Chi tiết đã ghi vào:\n{CrashLog.Path}");
    }

    internal static async Task ReportIfFaultedAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            CrashLog.Write("Background task", ex);
            ScreenshotCapture.ReportBackgroundFailure(ex);
        }
    }

    /// <summary>
    /// Opens the full panel beside the shelf card. No longer the way to see what the shelf holds —
    /// the card shows that itself — so this is only for what does not fit on it: several shelves,
    /// renaming, reordering.
    /// </summary>
    internal void OpenShelfPanel(BubbleWindow bubble) =>
        _ = ReportIfFaultedAsync(GetOrCreatePanelWindow()
            .ShowNextToAsync(bubble, Settings.EnabledEdges.FirstOrDefault(ScreenEdge.Right)));

    internal ShelfPanelWindow GetOrCreatePanelWindow()
    {
        _panelWindow ??= new ShelfPanelWindow(this);
        return _panelWindow;
    }

    private void ShowSettingsWindow()
    {
        _settingsWindow ??= new SettingsWindow(this);
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    /// <summary>Rebuilds the trigger and re-registers the hotkey after Settings changes (called from SettingsWindow).</summary>
    internal void ApplySettingsChanges()
    {
        SettingsPersistence.Save(Settings);

        // Dispose, not just Stop: the old instance owns a thread, two installed hooks and a
        // wait handle, and a settings save that only stopped it left all of that behind every
        // time. Disposing also joins the thread, so the old hooks are guaranteed uninstalled
        // before the new ones go in rather than both being live at once.
        _win32Window!.HotkeyPressed -= OnHotkeyPressed;
        _win32Window.ClipboardChanged -= OnClipboardChangedOnBackgroundThread;
        _win32Window.Dispose();

        // The trigger subscribes itself to the hooks in its constructor, so it belongs to the
        // instance it was built against and is replaced along with it.
        if (_dragTrigger is not null)
        {
            _dragTrigger.Triggered -= OnDragTriggered;
        }

        _win32Window = new Win32MessageWindow();
        _win32Window.RegisterHotkey(HistoryHotkeyId, Settings.HistoryHotkeyModifiers, Settings.HistoryHotkeyVirtualKey);
        _win32Window.RegisterHotkey(ShelfHotkeyId, Settings.ShelfHotkeyModifiers, Settings.ShelfHotkeyVirtualKey);
        _win32Window.HotkeyPressed += OnHotkeyPressed;
        _win32Window.ClipboardChanged += OnClipboardChangedOnBackgroundThread;
        _win32Window.CallbackFailed += (_, ex) => CrashLog.Write("Win32 callback", ex);

        _dragTrigger = new ShelfDragTrigger(
            BuildDragTriggerOptions(Settings),
            VirtualScreen.GetBounds,
            _win32Window.MouseHook,
            _win32Window.KeyboardHook);
        _dragTrigger.Triggered += OnDragTriggered;

        _win32Window.Start();

        StartupRegistration.SetEnabled(Settings.StartWithWindows);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _win32Window?.Dispose();
        _tray?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        base.OnExit(e);
    }
}

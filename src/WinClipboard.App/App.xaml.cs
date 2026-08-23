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
        _win32Window.HotkeyPressed += OnHotkeyPressed;
        _win32Window.ClipboardChanged += OnClipboardChangedOnBackgroundThread;

        _dragTrigger = new ShelfDragTrigger(
            BuildDragTriggerOptions(Settings),
            GetVirtualScreenBounds,
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

    private static ScreenRect _cachedScreenBounds;
    private static long _screenBoundsCachedAtTicks;

    /// <summary>
    /// Union of every monitor's bounds, so the edge trigger works on whichever screen the drag is
    /// happening on (plan 6: multi-monitor / mixed-DPI risk).
    ///
    /// Cached, because this is asked for on every mouse sample while a drag is in progress - which
    /// is up to a thousand times a second on a high-polling-rate mouse, on the thread the mouse
    /// hook runs on. The four SystemParameters reads behind it are not free, and the answer only
    /// changes when a monitor is added, removed or rearranged. A second of staleness after that is
    /// not something a drag can notice.
    /// </summary>
    private static ScreenRect GetVirtualScreenBounds()
    {
        var now = Environment.TickCount64;
        if (now - _screenBoundsCachedAtTicks < 1000 && _cachedScreenBounds.Right != 0)
        {
            return _cachedScreenBounds;
        }

        var left = (int)SystemParameters.VirtualScreenLeft;
        var top = (int)SystemParameters.VirtualScreenTop;
        var width = (int)SystemParameters.VirtualScreenWidth;
        var height = (int)SystemParameters.VirtualScreenHeight;

        _cachedScreenBounds = new ScreenRect(left, top, left + width, top + height);
        _screenBoundsCachedAtTicks = now;
        return _cachedScreenBounds;
    }

    // Everything below is raised on the Win32 message-loop thread, and that thread must never be
    // made to wait on the UI thread. It owns the two low-level hooks, and Windows delivers
    // WH_MOUSE_LL callbacks to it synchronously: if the thread is blocked, every mouse event on
    // the machine is blocked with it, and once a callback overruns LowLevelHooksTimeout (300ms
    // by default) Windows silently removes the hook, after which no trigger can ever fire again.
    // Showing a window comfortably exceeds that on its first call, when the XAML is parsed.
    // So these hand off with InvokeAsync and return immediately - never Invoke.

    private void OnHotkeyPressed(object? sender, int hotkeyId)
    {
        if (hotkeyId == HistoryHotkeyId)
        {
            Dispatcher.InvokeAsync(ToggleHistoryWindow);
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
        _ = ReportIfFaultedAsync(_bubbleWindow.ShowAtEdgeAsync(trigger.Edge ?? ScreenEdge.Right));
    }

    /// <summary>
    /// Opens the bubble and its panel from the tray. Unlike the drag triggers this happens with no
    /// drag in progress, so it goes straight to the expanded panel — the point is to look at what
    /// the shelf holds, not to catch an incoming drop.
    /// </summary>
    private void ShowShelfFromTray()
    {
        _bubbleWindow ??= new BubbleWindow(this);
        var edge = Settings.EnabledEdges.FirstOrDefault(ScreenEdge.Right);
        _ = ReportIfFaultedAsync(ShowShelfAsync(edge));
    }

    private async Task ShowShelfAsync(ScreenEdge edge)
    {
        await _bubbleWindow!.ShowAtEdgeAsync(edge, forceVisible: true);
        await GetOrCreatePanelWindow().ShowNextToAsync(_bubbleWindow, edge);
    }

    /// <summary>
    /// Awaits a fire-and-forget task and surfaces a failure instead of letting it vanish into an
    /// unobserved Task. Screenshot mode routes this to its log; a normal run shows a message box.
    /// </summary>
    internal static async Task ReportIfFaultedAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            ScreenshotCapture.ReportBackgroundFailure(ex);
        }
    }

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
        _win32Window.HotkeyPressed += OnHotkeyPressed;
        _win32Window.ClipboardChanged += OnClipboardChangedOnBackgroundThread;

        _dragTrigger = new ShelfDragTrigger(
            BuildDragTriggerOptions(Settings),
            GetVirtualScreenBounds,
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

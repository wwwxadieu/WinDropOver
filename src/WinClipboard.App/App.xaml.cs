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
    private EdgeAndHotkeyDragTrigger? _dragTrigger;
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

        _dragTrigger = new EdgeAndHotkeyDragTrigger(
            BuildDragTriggerOptions(Settings),
            GetVirtualScreenBounds,
            _win32Window.MouseHook,
            _win32Window.KeyboardHook);
        _dragTrigger.Triggered += OnDragTriggered;

        _win32Window.Start();

        StartupRegistration.SetEnabled(Settings.StartWithWindows);

        _tray = new TrayIconService();
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
        HoldKeyVirtualCode = ModifierHoldKeyMap.ToVirtualKeyCode(settings.HoldKey)
    };

    /// <summary>Union of every monitor's bounds, so the edge trigger works on whichever screen the drag is happening on (plan 6: multi-monitor / mixed-DPI risk).</summary>
    private static ScreenRect GetVirtualScreenBounds()
    {
        var left = (int)SystemParameters.VirtualScreenLeft;
        var top = (int)SystemParameters.VirtualScreenTop;
        var width = (int)SystemParameters.VirtualScreenWidth;
        var height = (int)SystemParameters.VirtualScreenHeight;
        return new ScreenRect(left, top, left + width, top + height);
    }

    private void OnHotkeyPressed(object? sender, int hotkeyId)
    {
        if (hotkeyId == HistoryHotkeyId)
        {
            Dispatcher.Invoke(ToggleHistoryWindow);
        }
    }

    private void OnClipboardChangedOnBackgroundThread(object? sender, EventArgs e)
    {
        // WM_CLIPBOARDUPDATE arrives on the Win32 message-loop thread; Clipboard reads must
        // happen on the WPF UI thread (see ClipboardMonitorService's class remarks).
        Dispatcher.Invoke(() => _clipboardMonitor!.OnClipboardChanged());
    }

    private void OnDragTriggered(object? sender, DragTriggerEventArgs e)
    {
        Dispatcher.Invoke(() => ShowBubble(e));
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
            _historyWindow.ShowOverlay();
        }
    }

    private void ShowBubble(DragTriggerEventArgs trigger)
    {
        _bubbleWindow ??= new BubbleWindow(this);
        _bubbleWindow.ShowAtEdge(trigger.Edge ?? ScreenEdge.Right);
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

        _win32Window!.HotkeyPressed -= OnHotkeyPressed;
        _win32Window.Stop();
        _win32Window = new Win32MessageWindow();
        _win32Window.RegisterHotkey(HistoryHotkeyId, Settings.HistoryHotkeyModifiers, Settings.HistoryHotkeyVirtualKey);
        _win32Window.HotkeyPressed += OnHotkeyPressed;
        _win32Window.ClipboardChanged += OnClipboardChangedOnBackgroundThread;

        _dragTrigger = new EdgeAndHotkeyDragTrigger(
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
        _win32Window?.Stop();
        _tray?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        base.OnExit(e);
    }
}

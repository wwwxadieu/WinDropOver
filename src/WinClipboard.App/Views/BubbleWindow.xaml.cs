using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using WinClipboard.Core.Models;
using WinClipboard.Interop;

namespace WinClipboard.App.Views;

/// <summary>
/// The pill/circle that appears at a screen edge once the Edge & Hotkey Drag Trigger fires
/// (plan 3.2 Overlay/Bubble Window). Stays visible at the edge for as long as the active shelf
/// has items, accepting further drops, until auto-hide kicks in.
/// </summary>
public partial class BubbleWindow : Window
{
    private readonly App _app;
    private readonly DispatcherTimer _autoHideTimer;
    private ScreenEdge _currentEdge = ScreenEdge.Right;

    public BubbleWindow(App app)
    {
        _app = app;
        InitializeComponent();

        _autoHideTimer = new DispatcherTimer();
        _autoHideTimer.Tick += OnAutoHideTick;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            WindowStyleHelper.MakeLayeredToolWindow(hwnd);
        }
        catch
        {
            // Best-effort: the window still behaves correctly without this, just with slightly
            // different Alt+Tab/taskbar visuals than a real tool window.
        }
    }

    public async void ShowAtEdge(ScreenEdge edge)
    {
        _currentEdge = edge;
        PositionAtEdge(edge);
        await RefreshCountAsync();
        Show();
        RestartAutoHideTimer();
    }

    private void PositionAtEdge(ScreenEdge edge)
    {
        var workArea = SystemParameters.WorkArea;
        const double margin = 4;

        (Left, Top) = edge switch
        {
            ScreenEdge.Left => (workArea.Left + margin, workArea.Top + (workArea.Height - Height) / 2),
            ScreenEdge.Right => (workArea.Right - Width - margin, workArea.Top + (workArea.Height - Height) / 2),
            ScreenEdge.Top => (workArea.Left + (workArea.Width - Width) / 2, workArea.Top + margin),
            ScreenEdge.Bottom => (workArea.Left + (workArea.Width - Width) / 2, workArea.Bottom - Height - margin),
            _ => (workArea.Right - Width - margin, workArea.Top + (workArea.Height - Height) / 2)
        };
    }

    private async Task RefreshCountAsync()
    {
        var shelfId = await _app.ShelfSession.EnsureDefaultShelfAsync();
        var items = await _app.ShelfSession.GetItemsAsync(shelfId);
        CountText.Text = items.Count.ToString();

        if (items.Count == 0 && _app.Settings.AutoHideBubbleWhenIdle)
        {
            Hide();
        }
    }

    private void RestartAutoHideTimer()
    {
        _autoHideTimer.Stop();
        if (!_app.Settings.AutoHideBubbleWhenIdle)
        {
            return;
        }
        _autoHideTimer.Interval = TimeSpan.FromSeconds(Math.Max(1, _app.Settings.AutoHideIdleSeconds));
        _autoHideTimer.Start();
    }

    private async void OnAutoHideTick(object? sender, EventArgs e)
    {
        _autoHideTimer.Stop();
        var shelfId = await _app.ShelfSession.EnsureDefaultShelfAsync();
        var items = await _app.ShelfSession.GetItemsAsync(shelfId);
        if (items.Count == 0)
        {
            Hide();
        }
    }

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        BubbleBorder.Opacity = 1.0;
        BubbleBorder.RenderTransform = new ScaleTransform(1.12, 1.12, Width / 2, Height / 2);
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        BubbleBorder.Opacity = 0.92;
        BubbleBorder.RenderTransform = null;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        BubbleBorder.Opacity = 0.92;
        BubbleBorder.RenderTransform = null;

        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        var paths = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        var shelfId = await _app.ShelfSession.EnsureDefaultShelfAsync();
        var order = 0;
        foreach (var path in paths)
        {
            await _app.ShelfSession.AddItemAsync(new ShelfItem
            {
                ShelfId = shelfId,
                Type = ShelfItemType.File,
                FilePath = path,
                AddedAt = DateTimeOffset.UtcNow,
                SortOrder = order++
            });
        }

        await RefreshCountAsync();
        RestartAutoHideTimer();
    }

    private void OnClicked(object sender, MouseButtonEventArgs e)
    {
        var panel = _app.GetOrCreatePanelWindow();
        panel.ShowNextTo(this, _currentEdge);
        RestartAutoHideTimer();
    }
}

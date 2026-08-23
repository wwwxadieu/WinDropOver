using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using WinClipboard.App.Services;
using WinClipboard.Core.Models;
using WinClipboard.Interop;

namespace WinClipboard.App.Views;

/// <summary>
/// The pill/circle that appears at a screen edge once <see cref="ShelfDragTrigger"/> fires
/// (plan 3.2 Overlay/Bubble Window). Stays visible at the edge for as long as the active shelf
/// has items, accepting further drops, until auto-hide kicks in.
/// </summary>
public partial class BubbleWindow : Window
{
    private const double CollapsedHeight = 72;

    /// <summary>
    /// Instant Actions offered when a drag hovers an empty shelf. Kept short deliberately —
    /// Dropover allows up to nine, but every extra tile is another thing to aim at while already
    /// holding a drag, so this is the subset that needs no prior configuration.
    /// </summary>
    private static readonly (QuickActionType Action, string Label)[] InstantActions =
    [
        (QuickActionType.Zip, "Nén ZIP"),
        (QuickActionType.CopyToFolder, "Sao chép vào..."),
        (QuickActionType.MoveToFolder, "Chuyển vào..."),
        (QuickActionType.CopyToClipboard, "Copy")
    ];

    private readonly App _app;
    private readonly DispatcherTimer _autoHideTimer;
    private ScreenEdge _currentEdge = ScreenEdge.Right;
    private bool _instantActionsVisible;

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

    /// <summary>
    /// Task-returning (not async void) so failures surface to the caller instead of crashing the
    /// process. <paramref name="forceVisible"/> is for opening from the tray, where the user asked
    /// to see the shelf and an empty one should still appear.
    /// </summary>
    public async Task ShowAtEdgeAsync(ScreenEdge edge, bool forceVisible = false)
    {
        _currentEdge = edge;
        PositionAtEdge(edge);

        var itemCount = await RefreshCountAsync();
        if (itemCount == 0 && !forceVisible && _app.Settings.AutoHideBubbleWhenIdle)
        {
            Hide();
            return;
        }

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

    /// <summary>Updates the badge and returns the item count. Deciding whether to hide is the caller's, so this cannot fight with a Show() that follows it.</summary>
    private async Task<int> RefreshCountAsync()
    {
        var shelfId = await _app.ShelfSession.EnsureDefaultShelfAsync();
        var items = await _app.ShelfSession.GetItemsAsync(shelfId);
        CountText.Text = items.Count.ToString();
        return items.Count;
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

    private async void OnDragEnter(object sender, DragEventArgs e)
    {
        SetDropEffect(e);
        BubbleBorder.Opacity = 1.0;
        BubbleBorder.RenderTransform = new ScaleTransform(1.12, 1.12, 36, 36);

        // Instant Actions only make sense on an empty shelf: once it holds something, a drop
        // means "add to the pile", and running an action would act on the pile too.
        var shelfId = await _app.ShelfSession.EnsureDefaultShelfAsync();
        var isEmpty = (await _app.ShelfSession.GetItemsAsync(shelfId)).Count == 0;
        if (isEmpty && ShelfDropReader.CanRead(e.Data))
        {
            ShowInstantActions();
        }
    }

    // WPF asks again on every move, so DragEnter alone is not enough to keep the copy cursor.
    private void OnDragOver(object sender, DragEventArgs e) => SetDropEffect(e);

    private static void SetDropEffect(DragEventArgs e)
    {
        e.Effects = ShelfDropReader.CanRead(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        BubbleBorder.Opacity = 0.92;
        BubbleBorder.RenderTransform = null;
        HideInstantActions();
    }

    private void ShowInstantActions()
    {
        if (_instantActionsVisible)
        {
            return;
        }

        InstantActionsList.Children.Clear();
        foreach (var (action, label) in InstantActions)
        {
            InstantActionsList.Children.Add(BuildActionTile(action, label));
        }

        InstantActionsPanel.Visibility = Visibility.Visible;
        _instantActionsVisible = true;

        // The window has to physically grow, otherwise the tiles fall outside it and never
        // receive the drop.
        InstantActionsPanel.UpdateLayout();
        Height = CollapsedHeight + InstantActionsPanel.ActualHeight + 6;
        KeepOnScreen();
    }

    private void HideInstantActions()
    {
        if (!_instantActionsVisible)
        {
            return;
        }
        InstantActionsPanel.Visibility = Visibility.Collapsed;
        _instantActionsVisible = false;
        Height = CollapsedHeight;
        PositionAtEdge(_currentEdge);
    }

    private Border BuildActionTile(QuickActionType action, string label)
    {
        var tile = new Border
        {
            Background = (Brush)FindResource("RowHoverBrush"),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 0, 0, 4),
            AllowDrop = true,
            Child = new TextBlock
            {
                Text = label,
                Foreground = (Brush)FindResource("TextPrimaryBrush"),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                IsHitTestVisible = false
            }
        };

        tile.DragEnter += (_, args) =>
        {
            tile.Background = (Brush)FindResource("AccentBrush");
            args.Effects = DragDropEffects.Copy;
            args.Handled = true;
        };
        tile.DragOver += (_, args) =>
        {
            args.Effects = DragDropEffects.Copy;
            args.Handled = true;
        };
        tile.DragLeave += (_, _) => tile.Background = (Brush)FindResource("RowHoverBrush");
        tile.Drop += async (_, args) =>
        {
            args.Handled = true;   // Stop the window-level Drop from also collecting these items.
            tile.Background = (Brush)FindResource("RowHoverBrush");
            await RunInstantActionAsync(action, args.Data);
        };

        return tile;
    }

    /// <summary>Collects the dropped items onto the shelf, then immediately runs the chosen action over them.</summary>
    private async Task RunInstantActionAsync(QuickActionType action, IDataObject data)
    {
        HideInstantActions();

        var shelfId = await _app.ShelfSession.EnsureDefaultShelfAsync();
        var items = ShelfDropReader.Read(data, shelfId, firstSortOrder: 0);
        foreach (var item in items)
        {
            await _app.ShelfSession.AddItemAsync(item);
        }

        var shelf = await _app.ShelfSession.GetShelfAsync(shelfId);
        var targetPath = shelf?.DefaultTargetPath;
        if (NeedsTargetFolder(action) && string.IsNullOrWhiteSpace(targetPath))
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Chọn thư mục đích" };
            if (dialog.ShowDialog() != true)
            {
                // Cancelled: the items stay on the shelf, so nothing the user dragged is lost.
                await RefreshCountAsync();
                return;
            }
            targetPath = dialog.FolderName;
        }

        var result = await _app.QuickActions.ExecuteAsync(shelfId, action, targetPath);

        // A move consumes the files, so anything that succeeded should leave the shelf.
        if (action == QuickActionType.MoveToFolder)
        {
            foreach (var itemResult in result.ItemResults.Where(r => r.Succeeded))
            {
                await _app.ShelfSession.RemoveItemAsync(itemResult.ShelfItemId);
            }
        }

        await RefreshCountAsync();
        RestartAutoHideTimer();
    }

    private static bool NeedsTargetFolder(QuickActionType action) =>
        action is QuickActionType.MoveToFolder or QuickActionType.CopyToFolder or QuickActionType.Zip;

    /// <summary>After the window grows downward it can run off the bottom of the work area; nudge it back up.</summary>
    private void KeepOnScreen()
    {
        var workArea = SystemParameters.WorkArea;
        if (Top + Height > workArea.Bottom)
        {
            Top = Math.Max(workArea.Top, workArea.Bottom - Height - 4);
        }
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        BubbleBorder.Opacity = 0.92;
        BubbleBorder.RenderTransform = null;
        // Only reached when the drop landed on the bubble itself; a drop on an action tile is
        // handled there and marked Handled so it never gets here.
        HideInstantActions();

        var shelfId = await _app.ShelfSession.EnsureDefaultShelfAsync();
        var existing = await _app.ShelfSession.GetItemsAsync(shelfId);
        var items = ShelfDropReader.Read(e.Data, shelfId, firstSortOrder: existing.Count);

        foreach (var item in items)
        {
            await _app.ShelfSession.AddItemAsync(item);
        }

        await RefreshCountAsync();
        RestartAutoHideTimer();
    }

    private void OnClicked(object sender, MouseButtonEventArgs e)
    {
        var panel = _app.GetOrCreatePanelWindow();
        _ = App.ReportIfFaultedAsync(panel.ShowNextToAsync(this, _currentEdge));
        RestartAutoHideTimer();
    }
}

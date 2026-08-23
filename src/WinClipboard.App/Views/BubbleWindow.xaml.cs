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
    /// Shows the shelf centred on a screen point, in the physical pixels the mouse hook reports.
    ///
    /// Centred on the cursor, not parked at a screen edge, because the gesture that summons this
    /// happens mid-drag: the user is already holding files somewhere in the middle of the screen.
    /// A shelf that appears at the edge means dragging all the way over to it before letting go,
    /// which is most of the work the shelf exists to save — and looks exactly like drag-and-drop
    /// being broken when you release where you are and nothing happens. Under the cursor, letting
    /// go without moving at all is a drop.
    ///
    /// Always shows, even with nothing on the shelf. It used to hide itself when the shelf was
    /// empty, which defeated the entire gesture: you shake while dragging a file precisely
    /// *because* the shelf is empty and you want to start filling it, so the one moment the shelf
    /// was most needed was the one moment it refused to appear. Auto-hide still exists, but it
    /// belongs to the idle timer below — hiding something the user just asked for is not
    /// auto-hide, it is not showing up.
    ///
    /// Task-returning (not async void) so failures surface to the caller instead of crashing the
    /// process.
    /// </summary>
    public async Task ShowAtPointAsync(int screenX, int screenY)
    {
        // Realise the handle without showing the window, so it can be positioned before it is
        // ever painted: showing first and moving after is a visible jump across the screen.
        var hwnd = new WindowInteropHelper(this).EnsureHandle();
        WindowPlacement.CentreOnScreenPoint(hwnd, screenX, screenY);

        await RefreshCountAsync();

        Show();
        // Position again after Show(): WPF applies its own placement as part of showing, which
        // would otherwise undo the one above.
        WindowPlacement.CentreOnScreenPoint(hwnd, screenX, screenY);
        RestartAutoHideTimer();
    }

    /// <summary>Shows the shelf wherever the pointer currently is — for the tray entry, which has no drag to take a position from.</summary>
    public Task ShowAtCursorAsync()
    {
        var (x, y) = WindowPlacement.GetCursorPosition();
        return ShowAtPointAsync(x, y);
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

        // Ask the data object what it holds *before* yielding. It belongs to the OLE drag loop,
        // which ends the moment this handler awaits, so anything read afterwards is read from a
        // payload that may already have been released.
        var canRead = ShelfDropReader.CanRead(e.Data);

        // Instant Actions only make sense on an empty shelf: once it holds something, a drop
        // means "add to the pile", and running an action would act on the pile too.
        var shelfId = await _app.ShelfSession.EnsureDefaultShelfAsync();
        var isEmpty = (await _app.ShelfSession.GetItemsAsync(shelfId)).Count == 0;
        if (isEmpty && canRead)
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
        // Collapsing shrinks the window upward from its top-left, which is where it already is,
        // so there is nothing to reposition — the shelf stays put under the cursor that summoned it.
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

        // Read the payload before the first await, for the reason given in OnDragEnter: the drag
        // loop owns it and lets go as soon as this handler yields. Reading it afterwards is how a
        // drop that visibly succeeded ends up adding nothing at all.
        var dropped = ShelfDropReader.Read(e.Data, shelfId: 0, firstSortOrder: 0);
        if (dropped.Count == 0)
        {
            return;
        }

        var shelfId = await _app.ShelfSession.EnsureDefaultShelfAsync();
        var firstSortOrder = (await _app.ShelfSession.GetItemsAsync(shelfId)).Count;

        foreach (var item in dropped)
        {
            // The real shelf and ordering are only knowable after the awaits above, so they are
            // stamped on here rather than at parse time.
            item.ShelfId = shelfId;
            item.SortOrder += firstSortOrder;
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

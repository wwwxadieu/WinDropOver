using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using WinClipboard.App.Services;
using WinClipboard.App.ViewModels;
using WinClipboard.Core.Models;
using WinClipboard.Interop;

namespace WinClipboard.App.Views;

/// <summary>
/// The shelf itself — a floating card that appears under the cursor when a drag trigger fires,
/// shows what it holds, and takes drops anywhere on its surface.
///
/// It used to be a 72px circle with a count on it, which meant the thing you dropped onto and
/// the thing that showed you the result were two different windows with a click between them.
/// Dropover has no such step: what appears mid-drag is already the shelf. So the card shows its
/// contents and carries the action bar, and the separate panel is now only for what does not fit
/// — several shelves, renaming, reordering.
/// </summary>
public partial class BubbleWindow : Window
{
    private readonly App _app;
    private readonly DispatcherTimer _autoHideTimer;
    private long _shelfId;

    /// <summary>When set, the card shows this shelf instead of the default one.</summary>
    private long? _pinnedShelfId;
    private Point? _dragStartPoint;
    private bool _suppressDeactivateHide;

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
            WindowStyleHelper.MakeLayeredToolWindow(new WindowInteropHelper(this).Handle);
        }
        catch
        {
            // Best-effort: without it the card still works, it just shows up in Alt+Tab.
        }
    }

    /// <summary>
    /// Shows the shelf centred on a screen point, in the physical pixels the mouse hook reports.
    ///
    /// Centred on the cursor rather than parked at a screen edge because the gesture that summons
    /// this happens mid-drag: letting go without moving at all has to be a drop, or the shelf
    /// costs more than it saves.
    /// </summary>
    public async Task ShowAtPointAsync(int screenX, int screenY)
    {
        // Realise the handle without showing, so the card can be positioned before it is painted:
        // showing first and moving after is a visible jump across the screen.
        var hwnd = new WindowInteropHelper(this).EnsureHandle();
        WindowPlacement.CentreOnScreenPoint(hwnd, screenX, screenY);

        await ReloadAsync();

        Show();
        // Again after Show(): WPF applies its own placement as part of showing.
        WindowPlacement.CentreOnScreenPoint(hwnd, screenX, screenY);
        RestartAutoHideTimer();
    }

    /// <summary>Shows a particular shelf rather than the default one — what the panel needs when the user switches tabs.</summary>
    public Task ShowShelfAsync(long shelfId, int screenX, int screenY)
    {
        _pinnedShelfId = shelfId;
        return ShowAtPointAsync(screenX, screenY);
    }

    /// <summary>Shows the shelf wherever the pointer currently is — for the tray entry, which has no drag to take a position from.</summary>
    public Task ShowAtCursorAsync()
    {
        var (x, y) = WindowPlacement.GetCursorPosition();
        return ShowAtPointAsync(x, y);
    }

    /// <summary>Wide enough for the grid tiles; the list reuses the same decode at a smaller draw size rather than decoding twice.</summary>
    private const int ThumbnailPixelWidth = 144;

    /// <summary>Reloads the card's contents. Returns the item count for callers that care.</summary>
    public async Task<int> ReloadAsync()
    {
        _shelfId = _pinnedShelfId ?? await _app.ShelfSession.EnsureDefaultShelfAsync();
        var shelf = await _app.ShelfSession.GetShelfAsync(_shelfId);
        var items = await _app.ShelfSession.GetItemsAsync(_shelfId);
        var views = await ShelfItemView.BuildAsync(items, ThumbnailPixelWidth);

        ShelfNameText.Text = shelf?.Name ?? "Shelf";
        CountText.Text = $"{items.Count} mục";
        EmptyState.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RecallHint.Visibility = items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        // Grid only when the shelf is nothing but images. Anything else goes to the list: one
        // document among the photos makes a grid of thumbnails misleading, since that item is the
        // one the grid cannot show, and a name is what identifies it.
        var allImages = views.Count > 0 && views.All(v => v.IsImage);
        ShowItems(allImages ? ThumbnailGrid : ItemsList, views);
        return items.Count;
    }

    private void ShowItems(ListBox visible, List<ShelfItemView> views)
    {
        var hidden = ReferenceEquals(visible, ItemsList) ? ThumbnailGrid : ItemsList;

        visible.ItemsSource = views;
        visible.Visibility = Visibility.Visible;

        // Release the other view's items rather than only hiding it: leaving both bound would
        // hold every decoded thumbnail twice for as long as the card lives.
        hidden.ItemsSource = null;
        hidden.Visibility = Visibility.Collapsed;
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
        // Only an empty shelf goes away on its own; one holding something is still wanted.
        if (await ReloadAsync() == 0 && !_suppressDeactivateHide)
        {
            Hide();
        }
    }

    /// <summary>
    /// Clicking away puts the shelf out of the way — it is a floating card over other people's
    /// windows, so leaving it parked there is its own kind of rude.
    ///
    /// What makes this safe to do is that it can be brought back: Ctrl+Shift+D reopens it at the
    /// pointer, which is where you need it, having navigated somewhere else in order to drag its
    /// contents out. Without that a shelf holding files would vanish on a stray click with no way
    /// back to it, so the two halves have to ship together.
    ///
    /// Nothing is discarded — the shelf keeps everything, this only hides the window.
    /// </summary>
    private void OnDeactivated(object? sender, EventArgs e)
    {
        // A modal folder picker deactivates this window too, and hiding the shelf out from under
        // the dialog the user opened from it would be absurd.
        if (!_suppressDeactivateHide)
        {
            Hide();
        }
    }

    // ---------------- Taking drops ----------------

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        SetDropEffect(e);
        RootBackground.BorderBrush = (Brush)FindResource("AccentBrush");
    }

    private void OnDragOver(object sender, DragEventArgs e) => SetDropEffect(e);

    private void OnDragLeave(object sender, DragEventArgs e) =>
        RootBackground.BorderBrush = (Brush)FindResource("SurfaceBorderBrush");

    private static void SetDropEffect(DragEventArgs e)
    {
        e.Effects = ShelfDropReader.CanRead(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        RootBackground.BorderBrush = (Brush)FindResource("SurfaceBorderBrush");

        // Read the payload before the first await: it belongs to the OLE drag loop, which lets go
        // the moment this handler yields, and reading it afterwards is how a drop that visibly
        // landed ends up adding nothing.
        var dropped = ShelfDropReader.Read(e.Data, shelfId: 0, firstSortOrder: 0);
        if (dropped.Count == 0)
        {
            return;
        }

        await AddToShelfAsync(dropped);
        RestartAutoHideTimer();
    }

    /// <summary>Stamps the real shelf and ordering onto freshly parsed items, which are only knowable after awaiting.</summary>
    private async Task AddToShelfAsync(List<ShelfItem> items)
    {
        var shelfId = await _app.ShelfSession.EnsureDefaultShelfAsync();
        var firstSortOrder = (await _app.ShelfSession.GetItemsAsync(shelfId)).Count;

        foreach (var item in items)
        {
            item.ShelfId = shelfId;
            item.SortOrder += firstSortOrder;
            item.Id = await _app.ShelfSession.AddItemAsync(item);
        }

        await ReloadAsync();
    }

    // ---------------- Dragging items back out ----------------

    private void OnItemPreviewMouseDown(object sender, MouseButtonEventArgs e) => _dragStartPoint = e.GetPosition(null);

    private void OnItemMouseUp(object sender, MouseButtonEventArgs e) => _dragStartPoint = null;

    private void OnItemMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStartPoint is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var diff = _dragStartPoint.Value - e.GetPosition(null);
        if (Math.Abs(diff.X) < 8 && Math.Abs(diff.Y) < 8)
        {
            return;
        }
        _dragStartPoint = null;

        if (e.OriginalSource is not DependencyObject source || FindItemView(source) is not { } view)
        {
            return;
        }

        var data = BuildDragData(view);
        if (data is not null)
        {
            // Whichever of the two views the drag started in is the drag source.
            var source = sender as DependencyObject ?? ItemsList;
            DragDrop.DoDragDrop(source, data, DragDropEffects.Copy | DragDropEffects.Move);
        }
    }

    private static ShelfItemView? FindItemView(DependencyObject source)
    {
        for (var current = source; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is FrameworkElement { DataContext: ShelfItemView view })
            {
                return view;
            }
        }
        return null;
    }

    /// <summary>Carries the standard shell formats so the drag works into Explorer, plus the item's own id so a drop onto one of this card's own action tiles can act on exactly that item.</summary>
    private static System.Windows.DataObject? BuildDragData(ShelfItemView view)
    {
        System.Windows.DataObject? data = view.Model.Type switch
        {
            ShelfItemType.File when view.Model.FilePath is not null =>
                new System.Windows.DataObject(DataFormats.FileDrop, new[] { view.Model.FilePath }),
            ShelfItemType.Text or ShelfItemType.Link when view.Model.TextContent is not null =>
                new System.Windows.DataObject(DataFormats.UnicodeText, view.Model.TextContent),
            _ => null
        };
        data?.SetData(ShelfItemIdsFormat, view.Model.Id.ToString());
        return data;
    }

    // ---------------- The action bar ----------------

    private const string ShelfItemIdsFormat = "WinClipboard.ShelfItemIds";

    private void OnActionDragEnter(object sender, DragEventArgs e)
    {
        SetActionDropEffect(sender, e);
        if (e.Effects != DragDropEffects.None && sender is Button button)
        {
            button.Background = (Brush)FindResource("AccentBrush");
        }
    }

    private void OnActionDragOver(object sender, DragEventArgs e) => SetActionDropEffect(sender, e);

    private void OnActionDragLeave(object sender, DragEventArgs e)
    {
        if (sender is Button button)
        {
            button.ClearValue(BackgroundProperty);
        }
    }

    private static void SetActionDropEffect(object sender, DragEventArgs e)
    {
        var accepted = e.Data.GetDataPresent(ShelfItemIdsFormat) || ShelfDropReader.CanRead(e.Data);
        e.Effects = accepted ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>Runs the dropped-on action against what was dragged: items from this shelf are matched by id, anything from outside is collected onto the shelf first so one gesture both collects and acts.</summary>
    private async void OnActionDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (sender is not Button { Tag: QuickActionType action } button)
        {
            return;
        }
        button.ClearValue(BackgroundProperty);

        // Everything read from the data object happens here, before the first await.
        var draggedIds = (e.Data.GetData(ShelfItemIdsFormat) as string)?
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => long.TryParse(t, out var id) ? id : (long?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToHashSet();
        List<ShelfItem> fromOutside = draggedIds is null
            ? ShelfDropReader.Read(e.Data, shelfId: 0, firstSortOrder: 0)
            : [];

        if (draggedIds is null)
        {
            await AddToShelfAsync(fromOutside);
        }

        var shelfItems = await _app.ShelfSession.GetItemsAsync(_shelfId);
        var target = draggedIds is not null
            ? shelfItems.Where(i => draggedIds.Contains(i.Id)).ToList()
            : fromOutside;

        await RunActionAsync(action, target);
    }

    private async void OnActionClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: QuickActionType action })
        {
            await RunActionAsync(action, await _app.ShelfSession.GetItemsAsync(_shelfId));
        }
    }

    private async Task RunActionAsync(QuickActionType action, IReadOnlyList<ShelfItem> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        var targetPath = await ResolveTargetPathAsync(action);
        if (targetPath is null && NeedsTargetFolder(action))
        {
            return;   // Folder picker cancelled; the shelf keeps everything.
        }

        var result = await _app.QuickActions.ExecuteOnItemsAsync(items, action, targetPath);

        if (action == QuickActionType.MoveToFolder)
        {
            foreach (var itemResult in result.ItemResults.Where(r => r.Succeeded))
            {
                await _app.ShelfSession.RemoveItemAsync(itemResult.ShelfItemId);
            }
        }

        await ReloadAsync();
        RestartAutoHideTimer();
    }

    private async Task<string?> ResolveTargetPathAsync(QuickActionType action)
    {
        if (!NeedsTargetFolder(action))
        {
            return null;
        }

        var shelf = await _app.ShelfSession.GetShelfAsync(_shelfId);
        if (!string.IsNullOrWhiteSpace(shelf?.DefaultTargetPath))
        {
            return shelf.DefaultTargetPath;
        }

        // The picker takes focus, which would otherwise let the idle timer decide the shelf had
        // been abandoned and hide it out from under the dialog.
        _suppressDeactivateHide = true;
        var dialog = new OpenFolderDialog { Title = "Chọn thư mục đích" };
        var picked = dialog.ShowDialog() == true;
        _suppressDeactivateHide = false;
        return picked ? dialog.FolderName : null;
    }

    private static bool NeedsTargetFolder(QuickActionType action) =>
        action is QuickActionType.MoveToFolder or QuickActionType.CopyToFolder or QuickActionType.Zip;

    private void OnOpenPanelClicked(object sender, RoutedEventArgs e) => _app.OpenShelfPanel(this);
}

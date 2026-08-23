using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using WinClipboard.App.Services;
using WinClipboard.App.ViewModels;
using WinClipboard.Core.Models;

namespace WinClipboard.App.Views;

/// <summary>
/// The expanded shelf view (plan 3.2 / section 4 "Panel Shelf — nhiều shelf + Quick Actions"):
/// a tab strip across shelves, the active shelf's items (draggable back out to Explorer/other
/// apps), and the Quick Actions bar for batch processing.
/// </summary>
public partial class ShelfPanelWindow : Window
{
    private static readonly string[] TabPalette = ["#3B82F6", "#10B981", "#F59E0B", "#EF4444", "#8B5CF6", "#EC4899"];

    private readonly App _app;
    private long _activeShelfId;
    private Point? _dragStartPoint;
    private bool _suppressDeactivateHide;

    public ShelfPanelWindow(App app)
    {
        _app = app;
        InitializeComponent();
    }

    /// <summary>Task-returning (not async void) so failures surface to the caller instead of crashing the process.</summary>
    public async Task ShowNextToAsync(BubbleWindow bubble, ScreenEdge edge)
    {
        if (IsVisible)
        {
            Hide();
            return;
        }

        const double gap = 8;
        (Left, Top) = edge switch
        {
            ScreenEdge.Left => (bubble.Left + bubble.Width + gap, bubble.Top),
            ScreenEdge.Right => (bubble.Left - Width - gap, bubble.Top),
            ScreenEdge.Top => (bubble.Left, bubble.Top + bubble.Height + gap),
            ScreenEdge.Bottom => (bubble.Left, bubble.Top - (Height > 0 ? Height : 400) - gap),
            _ => (bubble.Left - Width - gap, bubble.Top)
        };

        await LoadShelvesAsync();
        Show();
        Activate();
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (!_suppressDeactivateHide)
        {
            Hide();
        }
    }

    private async Task LoadShelvesAsync()
    {
        var shelves = await _app.ShelfSession.GetShelvesAsync();
        if (shelves.Count == 0)
        {
            await _app.ShelfSession.EnsureDefaultShelfAsync();
            shelves = await _app.ShelfSession.GetShelvesAsync();
        }

        if (shelves.All(s => s.Id != _activeShelfId))
        {
            _activeShelfId = shelves[0].Id;
        }

        ShelfTabsPanel.Children.Clear();
        foreach (var shelf in shelves)
        {
            var itemCount = (await _app.ShelfSession.GetItemsAsync(shelf.Id)).Count;
            ShelfTabsPanel.Children.Add(BuildTabButton(shelf, itemCount));
        }

        await LoadItemsAsync();
    }

    private Button BuildTabButton(Shelf shelf, int itemCount)
    {
        var isActive = shelf.Id == _activeShelfId;
        var brush = (Brush?)new BrushConverter().ConvertFromString(shelf.ColorHex) ?? Brushes.Gray;

        var button = new Button
        {
            Content = itemCount > 0 ? $"{shelf.Name} ({itemCount})" : shelf.Name,
            Background = brush,
            // Inactive tabs stay legible rather than fading out: the colour already says which
            // shelf is which, so the active one is marked by full opacity alone.
            Opacity = isActive ? 1.0 : 0.5,
            Style = (Style)FindResource("ShelfTabStyle"),
            Tag = shelf.Id
        };
        button.Click += async (_, _) =>
        {
            _activeShelfId = shelf.Id;
            await LoadShelvesAsync();
        };
        return button;
    }

    private async Task LoadItemsAsync()
    {
        var items = await _app.ShelfSession.GetItemsAsync(_activeShelfId);
        var views = items.Select(ShelfItemView.From).ToList();
        ItemsList.ItemsSource = views;
        EmptyItemsText.Visibility = views.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ItemsList.Visibility = views.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void OnAddShelfClicked(object sender, RoutedEventArgs e)
    {
        var existing = await _app.ShelfSession.GetShelvesAsync();
        var color = TabPalette[existing.Count % TabPalette.Length];
        var newId = await _app.ShelfSession.CreateShelfAsync(new Shelf
        {
            Name = $"Shelf {existing.Count + 1}",
            ColorHex = color,
            SortOrder = existing.Count,
            IsPersisted = _app.Settings.NewShelvesPersistByDefault
        });
        _activeShelfId = newId;
        await LoadShelvesAsync();
    }

    private async void OnRemoveItemClicked(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not ShelfItemView view)
        {
            return;
        }
        await _app.ShelfSession.RemoveItemAsync(view.Model.Id);
        await LoadShelvesAsync();
    }

    private async void OnMoveItemUpClicked(object sender, RoutedEventArgs e) => await MoveItemAsync(sender, -1);

    private async void OnMoveItemDownClicked(object sender, RoutedEventArgs e) => await MoveItemAsync(sender, +1);

    /// <summary>
    /// Swaps an item with its neighbour. SortOrder values in the database are not guaranteed to be
    /// a clean 0..n-1 run (items are deleted, and older drops all numbered from zero), so the swap
    /// is done on positions in the loaded list and both items are then renumbered from it.
    /// </summary>
    private async Task MoveItemAsync(object sender, int offset)
    {
        if (((FrameworkElement)sender).DataContext is not ShelfItemView view)
        {
            return;
        }

        var items = (await _app.ShelfSession.GetItemsAsync(_activeShelfId)).ToList();
        var index = items.FindIndex(i => i.Id == view.Model.Id);
        var targetIndex = index + offset;
        if (index < 0 || targetIndex < 0 || targetIndex >= items.Count)
        {
            return;
        }

        (items[index], items[targetIndex]) = (items[targetIndex], items[index]);
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].SortOrder != i)
            {
                items[i].SortOrder = i;
                await _app.ShelfSession.UpdateItemAsync(items[i]);
            }
        }

        await LoadShelvesAsync();
    }

    private async void OnRenameItemClicked(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not ShelfItemView view)
        {
            return;
        }

        var item = view.Model;
        _suppressDeactivateHide = true;
        try
        {
            if (item.Type == ShelfItemType.File)
            {
                await RenameFileItemAsync(item);
            }
            else
            {
                var newText = RenameDialog.Prompt(this, "Nội dung mới:", item.TextContent ?? string.Empty);
                if (newText is null)
                {
                    return;
                }
                item.TextContent = newText;
                await _app.ShelfSession.UpdateItemAsync(item);
            }
        }
        finally
        {
            _suppressDeactivateHide = false;
        }

        await LoadShelvesAsync();
    }

    /// <summary>Renames the file on disk, matching Dropover — a rename that only changed a label in the shelf would not survive dragging the file out.</summary>
    private async Task RenameFileItemAsync(ShelfItem item)
    {
        var currentPath = item.FilePath;
        if (string.IsNullOrEmpty(currentPath))
        {
            return;
        }

        var newName = RenameDialog.Prompt(this, "Tên tệp mới:", Path.GetFileName(currentPath));
        if (newName is null)
        {
            return;
        }

        var directory = Path.GetDirectoryName(currentPath);
        if (string.IsNullOrEmpty(directory))
        {
            StatusText.Text = "Không xác định được thư mục chứa tệp.";
            return;
        }

        var newPath = Path.Combine(directory, newName);
        try
        {
            File.Move(currentPath, newPath, overwrite: false);
        }
        catch (Exception ex)
        {
            // Locked file, missing permission, name already taken, invalid characters...
            StatusText.Text = $"Không đổi tên được: {ex.Message}";
            return;
        }

        item.FilePath = newPath;
        await _app.ShelfSession.UpdateItemAsync(item);
        StatusText.Text = $"Đã đổi tên thành {newName}.";
    }

    private void OnItemPreviewMouseDown(object sender, MouseButtonEventArgs e) => _dragStartPoint = e.GetPosition(null);

    private void OnItemMouseUp(object sender, MouseButtonEventArgs e) => _dragStartPoint = null;

    private void OnItemMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStartPoint is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(null);
        var diff = _dragStartPoint.Value - current;
        if (Math.Abs(diff.X) < 8 && Math.Abs(diff.Y) < 8)
        {
            return;
        }
        _dragStartPoint = null;

        if (e.OriginalSource is not DependencyObject source)
        {
            return;
        }
        var view = FindShelfItemView(source);
        if (view is null)
        {
            return;
        }

        var data = BuildDragData(view);
        if (data is not null)
        {
            DragDrop.DoDragDrop(ItemsList, data, DragDropEffects.Copy | DragDropEffects.Move);
        }
    }

    private static ShelfItemView? FindShelfItemView(DependencyObject source)
    {
        var current = source;
        while (current is not null)
        {
            if (current is FrameworkElement { DataContext: ShelfItemView view })
            {
                return view;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    /// <summary>
    /// Builds the payload for dragging an item out of the shelf. It carries two things: the
    /// standard shell formats, so the drag works into Explorer and other applications, and the
    /// item's own id, so dropping it back onto one of this panel's action tiles can act on
    /// exactly that item instead of re-deriving it from a file path.
    /// </summary>
    // Single-item drag-out for now — dragging the whole active shelf as one gesture (plan:
    // "kéo cả nhóm... ra nơi cần") is a reasonable follow-up once multi-select lands.
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

        // Ids travel as text: a drop onto our own window is in-process, but keeping the payload
        // to a primitive avoids relying on how WPF serialises richer types across formats.
        data?.SetData(ShelfItemIdsFormat, view.Model.Id.ToString());
        return data;
    }

    // ---------------- Quick Actions as drop targets ----------------

    /// <summary>Private clipboard format carrying the dragged shelf item's id back to this window.</summary>
    private const string ShelfItemIdsFormat = "WinClipboard.ShelfItemIds";

    private void OnQuickActionDragEnter(object sender, DragEventArgs e)
    {
        SetQuickActionDropEffect(sender, e);
        if (e.Effects != DragDropEffects.None && sender is Button button)
        {
            button.Background = (Brush)FindResource("AccentBrush");
        }
    }

    private void OnQuickActionDragOver(object sender, DragEventArgs e) => SetQuickActionDropEffect(sender, e);

    private void OnQuickActionDragLeave(object sender, DragEventArgs e)
    {
        if (sender is Button button)
        {
            // Clearing the local value lets the style's own brush take over again.
            button.ClearValue(BackgroundProperty);
        }
    }

    private static void SetQuickActionDropEffect(object sender, DragEventArgs e)
    {
        var accepted = e.Data.GetDataPresent(ShelfItemIdsFormat) || ShelfDropReader.CanRead(e.Data);
        e.Effects = accepted ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>
    /// Runs the dropped-on action against what was dragged. Items dragged from this shelf are
    /// matched by id; anything dragged in from outside is added to the shelf first, so the
    /// gesture both collects and acts in one motion.
    /// </summary>
    private async void OnQuickActionDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (sender is not Button { Tag: QuickActionType action } button)
        {
            return;
        }
        button.ClearValue(BackgroundProperty);

        // Read the data object before the first await: it belongs to the OLE drag loop, which
        // ends the moment this handler yields, after which the payload may no longer be readable.
        var droppedIds = (e.Data.GetData(ShelfItemIdsFormat) as string)?
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => long.TryParse(t, out var id) ? id : (long?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToHashSet();
        List<ShelfItem> droppedFromOutside = droppedIds is null
            ? ShelfDropReader.Read(e.Data, _activeShelfId, firstSortOrder: 0)
            : [];

        var items = await ResolveDroppedItemsAsync(droppedIds, droppedFromOutside);
        if (items.Count == 0)
        {
            StatusText.Text = "Không có mục nào để xử lý.";
            return;
        }

        var targetPath = await ResolveTargetPathAsync(action);
        if (targetPath is null && NeedsTargetFolder(action))
        {
            return;   // Cancelled the folder picker; items stay on the shelf.
        }

        var result = await _app.QuickActions.ExecuteOnItemsAsync(items, action, targetPath);

        if (action == QuickActionType.MoveToFolder)
        {
            foreach (var itemResult in result.ItemResults.Where(r => r.Succeeded))
            {
                await _app.ShelfSession.RemoveItemAsync(itemResult.ShelfItemId);
            }
        }

        StatusText.Text = result.AllSucceeded
            ? $"Xong: {result.SuccessCount} mục."
            : $"{result.SuccessCount} thành công, {result.FailureCount} lỗi.";

        await LoadShelvesAsync();
    }

    /// <summary>
    /// Resolves a drop to the items the action should run on. A drag that started in this shelf
    /// arrives as <paramref name="draggedIds"/> and is matched against what the shelf holds;
    /// anything else arrives already parsed in <paramref name="fromOutside"/> and is added to the
    /// shelf first, so one gesture both collects and acts.
    /// </summary>
    private async Task<IReadOnlyList<ShelfItem>> ResolveDroppedItemsAsync(
        HashSet<long>? draggedIds, List<ShelfItem> fromOutside)
    {
        if (draggedIds is not null)
        {
            return (await _app.ShelfSession.GetItemsAsync(_activeShelfId))
                .Where(i => draggedIds.Contains(i.Id))
                .ToList();
        }

        // Sort order was parsed as 0-based; rebase it so these land after what is already there.
        var firstSortOrder = (await _app.ShelfSession.GetItemsAsync(_activeShelfId)).Count;
        foreach (var item in fromOutside)
        {
            item.SortOrder += firstSortOrder;
            item.Id = await _app.ShelfSession.AddItemAsync(item);
        }
        return fromOutside;
    }

    /// <summary>Returns the folder for actions that need one, or null if the user cancelled. Actions that need no folder return null too — check <see cref="NeedsTargetFolder"/>.</summary>
    private async Task<string?> ResolveTargetPathAsync(QuickActionType action)
    {
        if (!NeedsTargetFolder(action))
        {
            return null;
        }

        var shelf = await _app.ShelfSession.GetShelfAsync(_activeShelfId);
        if (!string.IsNullOrWhiteSpace(shelf?.DefaultTargetPath))
        {
            return shelf.DefaultTargetPath;
        }

        var dialog = new OpenFolderDialog { Title = "Chọn thư mục đích" };
        _suppressDeactivateHide = true;
        var picked = dialog.ShowDialog() == true;
        _suppressDeactivateHide = false;
        return picked ? dialog.FolderName : null;
    }

    private static bool NeedsTargetFolder(QuickActionType action) =>
        action is QuickActionType.MoveToFolder or QuickActionType.CopyToFolder or QuickActionType.Zip;

    // ---------------- Quick Actions as buttons (whole shelf) ----------------

    private async void OnMoveClicked(object sender, RoutedEventArgs e) => await RunQuickActionAsync(QuickActionType.MoveToFolder, removeSucceededItems: true);

    private async void OnCopyToFolderClicked(object sender, RoutedEventArgs e) => await RunQuickActionAsync(QuickActionType.CopyToFolder, removeSucceededItems: false);

    private async void OnZipClicked(object sender, RoutedEventArgs e) => await RunQuickActionAsync(QuickActionType.Zip, removeSucceededItems: false);

    private async void OnCopyToClipboardClicked(object sender, RoutedEventArgs e) => await RunQuickActionAsync(QuickActionType.CopyToClipboard, removeSucceededItems: false);

    private async Task RunQuickActionAsync(QuickActionType actionType, bool removeSucceededItems)
    {
        var targetPath = await ResolveTargetPathAsync(actionType);
        if (targetPath is null && NeedsTargetFolder(actionType))
        {
            return;   // Cancelled the folder picker.
        }

        var result = await _app.QuickActions.ExecuteAsync(_activeShelfId, actionType, targetPath);

        if (removeSucceededItems)
        {
            foreach (var itemResult in result.ItemResults.Where(r => r.Succeeded))
            {
                await _app.ShelfSession.RemoveItemAsync(itemResult.ShelfItemId);
            }
        }

        StatusText.Text = result.AllSucceeded
            ? $"Xong: {result.SuccessCount} mục thành công."
            : $"{result.SuccessCount} thành công, {result.FailureCount} lỗi.";

        await LoadShelvesAsync();
    }
}

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
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

    // Single-item drag-out for now — dragging the whole active shelf as one gesture (plan:
    // "kéo cả nhóm... ra nơi cần") is a reasonable follow-up once multi-select lands.
    private static System.Windows.DataObject? BuildDragData(ShelfItemView view) => view.Model.Type switch
    {
        ShelfItemType.File when view.Model.FilePath is not null =>
            new System.Windows.DataObject(DataFormats.FileDrop, new[] { view.Model.FilePath }),
        ShelfItemType.Text or ShelfItemType.Link when view.Model.TextContent is not null =>
            new System.Windows.DataObject(DataFormats.UnicodeText, view.Model.TextContent),
        _ => null
    };

    private async void OnMoveClicked(object sender, RoutedEventArgs e) => await RunQuickActionAsync(QuickActionType.MoveToFolder, removeSucceededItems: true);

    private async void OnCopyToFolderClicked(object sender, RoutedEventArgs e) => await RunQuickActionAsync(QuickActionType.CopyToFolder, removeSucceededItems: false);

    private async void OnZipClicked(object sender, RoutedEventArgs e) => await RunQuickActionAsync(QuickActionType.Zip, removeSucceededItems: false);

    private async void OnCopyToClipboardClicked(object sender, RoutedEventArgs e) => await RunQuickActionAsync(QuickActionType.CopyToClipboard, removeSucceededItems: false);

    private async Task RunQuickActionAsync(QuickActionType actionType, bool removeSucceededItems)
    {
        string? targetPath = null;
        if (actionType is QuickActionType.MoveToFolder or QuickActionType.CopyToFolder or QuickActionType.Zip)
        {
            var dialog = new OpenFolderDialog { Title = "Chọn thư mục đích" };
            _suppressDeactivateHide = true;
            var picked = dialog.ShowDialog() == true;
            _suppressDeactivateHide = false;
            if (!picked)
            {
                return;
            }
            targetPath = dialog.FolderName;
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

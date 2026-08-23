using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WinClipboard.App.ViewModels;
using WinClipboard.Core.Models;
using WinClipboard.Interop;

namespace WinClipboard.App.Views;

public partial class HistoryOverlayWindow : Window
{
    private readonly App _app;
    private IntPtr _restoreFocusHandle;
    private ContentType? _activeFilter;

    /// <summary>
    /// False until the constructor finishes. The XAML sets IsChecked="True" on the "Tất cả"
    /// filter, and WPF raises Checked during InitializeComponent — at that point the controls
    /// declared further down the XAML (ItemsList, EmptyState) do not exist yet, so a handler
    /// that reloaded immediately would dereference null and, being async void, take the whole
    /// process down on the very first Ctrl+Shift+V.
    /// </summary>
    private readonly bool _initialized;

    public HistoryOverlayWindow(App app)
    {
        _app = app;
        InitializeComponent();
        _initialized = true;
    }

    /// <summary>
    /// Returns a Task rather than being async void so callers can await it and, more importantly,
    /// so a failure surfaces as a faulted Task instead of an unhandled exception that kills the
    /// process (which is exactly what async void does).
    /// </summary>
    public async Task ShowOverlayAsync()
    {
        // Must capture the caller's window *before* this window steals focus, so paste can send
        // the keystroke back to wherever the user actually was.
        _restoreFocusHandle = ForegroundWindowInfo.CaptureHandle();

        PositionNearBottomRight();
        await ReloadAsync();

        Show();
        Activate();
        SearchBox.Focus();
    }

    public void HideOverlay() => Hide();

    private void OnDeactivated(object? sender, EventArgs e) => Hide();

    private void PositionNearBottomRight()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 16;
        Top = workArea.Bottom - (Height > 0 ? Height : 480) - 16;
    }

    private async Task ReloadAsync()
    {
        var searchText = string.IsNullOrWhiteSpace(SearchBox.Text) ? null : SearchBox.Text.Trim();
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;

        var items = await _app.ClipboardRepository.QueryAsync(_activeFilter, searchText, limit: _app.Settings.MaxHistoryItems);
        var views = items.Select(ClipboardItemView.From).ToList();

        ItemsList.ItemsSource = views;
        EmptyState.Visibility = views.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ItemsList.Visibility = views.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_initialized)
        {
            return;
        }
        await ReloadAsync();
    }

    private async void OnFilterChanged(object sender, RoutedEventArgs e)
    {
        _activeFilter = sender switch
        {
            _ when ReferenceEquals(sender, FilterText) => ContentType.Text,
            _ when ReferenceEquals(sender, FilterImage) => ContentType.Image,
            _ when ReferenceEquals(sender, FilterFile) => ContentType.File,
            _ when ReferenceEquals(sender, FilterLink) => ContentType.Link,
            _ => null
        };

        // Fires once during InitializeComponent (see _initialized). The filter above is still
        // worth recording — it is just the default — but the reload has to wait until the
        // controls it writes to exist. ShowOverlayAsync reloads anyway before showing.
        if (!_initialized)
        {
            return;
        }
        await ReloadAsync();
    }

    private async void OnItemClicked(object sender, MouseButtonEventArgs e)
    {
        if (ItemsList.SelectedItem is not ClipboardItemView view)
        {
            return;
        }

        Hide();
        await _app.PasteService.PasteAsync(view.Model, _restoreFocusHandle);
    }

    private async void OnPinClicked(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (((FrameworkElement)sender).DataContext is not ClipboardItemView view)
        {
            return;
        }
        await _app.ClipboardRepository.SetPinnedAsync(view.Model.Id, !view.Model.IsPinned);
        await ReloadAsync();
    }

    private async void OnDeleteClicked(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (((FrameworkElement)sender).DataContext is not ClipboardItemView view)
        {
            return;
        }
        await _app.ClipboardRepository.DeleteAsync(view.Model.Id);
        await ReloadAsync();
    }
}

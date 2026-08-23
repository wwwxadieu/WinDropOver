using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using WinClipboard.App.ViewModels;
using WinClipboard.Core.Models;
using WinClipboard.Interop;

namespace WinClipboard.App.Views;

public partial class HistoryOverlayWindow : Window
{
    private readonly App _app;
    private readonly DispatcherTimer _strandedCheckTimer;
    private IntPtr _restoreFocusHandle;
    private DateTimeOffset _shownAt;
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
        _strandedCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _strandedCheckTimer.Tick += OnStrandedCheck;
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

        // Not Activate(). This runs from a global hotkey while another application is in the
        // foreground, and WPF's Activate() is a plain SetForegroundWindow, which Windows declines
        // for a background process — quietly. The overlay would appear, never become active, and
        // therefore never raise Deactivated, which is the only thing that closes it.
        ForegroundWindowInfo.ForceForeground(new WindowInteropHelper(this).Handle);
        SearchBox.Focus();

        _shownAt = DateTimeOffset.UtcNow;
        _strandedCheckTimer.Start();
    }

    public void HideOverlay() => Hide();

    private void OnDeactivated(object? sender, EventArgs e) => Hide();

    /// <summary>Stops the watchdog whichever way the window went away — Deactivated, Escape, a click, or the hotkey.</summary>
    protected override void OnClosed(EventArgs e)
    {
        _strandedCheckTimer.Stop();
        base.OnClosed(e);
    }

    /// <summary>
    /// The backstop for a window that is topmost, absent from the taskbar and Alt+Tab, and closes
    /// only on Deactivated: if it somehow never took the foreground, nothing would ever deactivate
    /// it and it would sit over the user's work with no way to dismiss it. That is not a state to
    /// leave reachable, however unlikely the path to it — so once a second, a window that is
    /// neither in front nor under the pointer puts itself away.
    ///
    /// The grace period covers the moment right after Show(), before the foreground change has
    /// been processed.
    /// </summary>
    private void OnStrandedCheck(object? sender, EventArgs e)
    {
        if (!IsVisible)
        {
            _strandedCheckTimer.Stop();
            return;
        }

        if (DateTimeOffset.UtcNow - _shownAt < TimeSpan.FromSeconds(1.5))
        {
            return;
        }

        if (!ForegroundWindowInfo.IsForeground(new WindowInteropHelper(this).Handle) && !IsMouseOver)
        {
            Hide();
        }
    }

    /// <summary>Escape closes it. Obvious for a transient panel, and a second way out that does not depend on focus behaving.</summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Hide();
        }
    }

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

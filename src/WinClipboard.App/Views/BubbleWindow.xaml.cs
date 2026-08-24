using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
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

    /// <summary>Distinguishes a drag that has left the card from one that merely crossed between its children — see OnDragLeave.</summary>
    private readonly DispatcherTimer _dragGoneTimer;
    private long _shelfId;

    /// <summary>When set, the card shows this shelf instead of the default one.</summary>
    private long? _pinnedShelfId;
    private Point? _dragStartPoint;

    /// <summary>What the card is showing right now. Kept because starting a drag has to build its payload synchronously — DoDragDrop blocks for the length of the gesture, so there is no awaiting the repository in the middle of one.</summary>
    private List<ShelfItemView> _currentViews = [];
    private bool _suppressDeactivateHide;

    public BubbleWindow(App app)
    {
        _app = app;
        InitializeComponent();

        _autoHideTimer = new DispatcherTimer();
        _autoHideTimer.Tick += OnAutoHideTick;

        // Short enough that a drag really leaving the card folds the panel away without a visible
        // pause; long enough that the gap between one DragOver and the next never expires it.
        _dragGoneTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _dragGoneTimer.Tick += (_, _) =>
        {
            _dragGoneTimer.Stop();
            CollapseActions();
        };
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
        _closing = false;

        // Realise the handle without showing, so the card can be positioned before it is painted:
        // showing first and moving after is a visible jump across the screen.
        var hwnd = new WindowInteropHelper(this).EnsureHandle();

        if (IsVisible)
        {
            // Already on screen and about to move: it has to come down first, or it photographs
            // itself for its own backdrop. The gap is one compositor frame — long enough for the
            // desktop underneath to have repainted, short enough that nobody sees a blink.
            Hide();
            await Task.Delay(16);
        }

        // Hidden until the opening animation runs it up from nothing, so the intermediate
        // position WPF picks during Show() is never visible. The clock has to be cleared first:
        // an animation left holding its final value outranks anything assigned to the property.
        CardRoot.BeginAnimation(OpacityProperty, null);
        CardRoot.Opacity = 0;
        WindowPlacement.CentreOnScreenPoint(hwnd, screenX, screenY);

        await ReloadAsync();
        CaptureBackdrop(hwnd);

        Show();
        // Again after Show(): WPF applies its own placement as part of showing. It lands on the
        // same rectangle as the first call — same point, same size — so the capture stays aligned.
        WindowPlacement.CentreOnScreenPoint(hwnd, screenX, screenY);

        PlayOpenAnimation();
        RestartAutoHideTimer();
    }

    // ---------------- Glass ----------------

    /// <summary>Matches the CardRoot margin in XAML: the gap between the window edge and the card.</summary>
    private const double CardMargin = 12;

    /// <summary>Matches the negative margin on BackdropImage: how far past the card the capture reaches.</summary>
    private const double BackdropBleed = 40;

    /// <summary>
    /// Takes the picture the glass is made of. Everything here is best-effort: any failure just
    /// leaves the card on its opaque fallback fill, which is what it looked like before.
    /// </summary>
    private void CaptureBackdrop(IntPtr hwnd)
    {
        BackdropImage.Source = null;
        BackdropImage.Visibility = Visibility.Collapsed;
        GlassTint.Fill = (Brush)FindResource("GlassBrush");

        try
        {
            if (WindowPlacement.GetWindowScreenRect(hwnd) is not { } window)
            {
                return;
            }

            // The card sits inside the window by CardMargin, and the capture reaches BackdropBleed
            // past the card on every side. Both are WPF units; the screen rectangle is physical
            // pixels, so each has to be scaled before it can be subtracted from one.
            var dpi = VisualTreeHelper.GetDpi(this);
            var insetX = (int)Math.Round((CardMargin - BackdropBleed) * dpi.DpiScaleX);
            var insetY = (int)Math.Round((CardMargin - BackdropBleed) * dpi.DpiScaleY);

            var region = ScreenCapture.Capture(
                window.Left + insetX,
                window.Top + insetY,
                window.Right - window.Left - insetX * 2,
                window.Bottom - window.Top - insetY * 2);
            if (region is null)
            {
                return;
            }

            // Bgr32 rather than Bgra32: BitBlt leaves the fourth byte of each pixel at zero, which
            // read as alpha would make the whole capture invisible.
            var image = BitmapSource.Create(
                region.Width, region.Height, 96, 96, PixelFormats.Bgr32, null,
                region.Pixels, region.Width * 4);
            image.Freeze();

            BackdropImage.Source = image;
            BackdropImage.Visibility = Visibility.Visible;
            GlassTint.Fill = (Brush)FindResource("GlassTintBrush");
        }
        catch
        {
            // Fallback fill is already in place from the top of this method.
        }
    }

    // ---------------- Opening and closing ----------------

    private bool _closing;

    /// <summary>
    /// The card grows in from slightly small and slightly low, with a touch of overshoot. This is
    /// not decoration: the shelf appears unannounced, in the middle of a drag, over whatever the
    /// user was looking at. A window that simply exists between one frame and the next reads as a
    /// glitch, where one that arrives reads as a thing that came from somewhere.
    ///
    /// Kept under a fifth of a second — long enough to be seen, short enough that a drop landing
    /// immediately after the trigger never has to wait for it.
    /// </summary>
    private void PlayOpenAnimation()
    {
        IsHitTestVisible = true;

        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(120))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var grow = new DoubleAnimation(0.92, 1, TimeSpan.FromMilliseconds(190))
        {
            // A small overshoot; the 12px margin around the card is what it grows into.
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.25 }
        };
        var rise = new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(190))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        CardRoot.BeginAnimation(OpacityProperty, fade);
        CardScale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        CardScale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        CardSlide.BeginAnimation(TranslateTransform.YProperty, rise);
    }

    /// <summary>
    /// The reverse, faster — going away should not hold anyone up. The window itself stays up
    /// until the animation finishes, so it stops taking clicks the moment the close begins rather
    /// than swallowing whatever the user was reaching for behind it.
    /// </summary>
    private void HideWithAnimation()
    {
        if (_closing || !IsVisible)
        {
            return;
        }
        _closing = true;
        CollapseActions();
        // Going away is an answer too, and the answer is no.
        AnswerDelete(null);
        // The whole window, not just the card: its background is Transparent, which in WPF is
        // still a surface that swallows clicks.
        IsHitTestVisible = false;

        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(110))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        fade.Completed += (_, _) =>
        {
            // A reopen during the close clears the flag, and this then has nothing to do: hiding
            // here would put away the card that was just asked for.
            if (_closing)
            {
                _closing = false;
                Hide();
            }
        };
        var shrink = new DoubleAnimation(0.94, TimeSpan.FromMilliseconds(110))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        CardRoot.BeginAnimation(OpacityProperty, fade);
        CardScale.BeginAnimation(ScaleTransform.ScaleXProperty, shrink);
        CardScale.BeginAnimation(ScaleTransform.ScaleYProperty, shrink);
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
        var items = await _app.ShelfSession.GetItemsAsync(_shelfId);
        var views = await ShelfItemView.BuildAsync(items, ThumbnailPixelWidth);
        _currentViews = views;

        EmptyState.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RecallHint.Visibility = items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        // Nothing to clear on an empty shelf, and a live-looking button that does nothing is
        // worse than no button.
        ClearButton.Visibility = items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        SummaryPill.Visibility = items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        SummaryText.Text = Summarise(views);
        ShowContents(views);
        return items.Count;
    }

    /// <summary>
    /// Names the pile the way someone would out loud — "3 tài liệu", "5 ảnh" — falling back to the
    /// neutral word once it holds more than one kind of thing.
    /// </summary>
    private static string Summarise(List<ShelfItemView> views)
    {
        if (views.Count == 0)
        {
            return "0 mục";
        }
        if (views.All(v => v.IsImage))
        {
            return $"{views.Count} ảnh";
        }
        if (views.All(v => v.Model.Type == ShelfItemType.File))
        {
            return $"{views.Count} tài liệu";
        }
        return $"{views.Count} mục";
    }

    /// <summary>Whether the pill has been used to open the detail list. Resets when the shelf empties, so the next drag starts on the stack again.</summary>
    private bool _showDetails;

    private void ShowContents(List<ShelfItemView> views)
    {
        if (views.Count == 0)
        {
            _showDetails = false;
        }

        // Grid only when the shelf is nothing but images. Anything else goes to the list: one
        // document among the photos makes a grid of thumbnails misleading, since that item is the
        // one the grid cannot show, and a name is what identifies it.
        var allImages = views.Count > 0 && views.All(v => v.IsImage);
        var detailView = allImages ? ThumbnailGrid : ItemsList;
        var otherView = allImages ? ItemsList : ThumbnailGrid;

        // Release the hidden views' items rather than only hiding them: leaving them bound would
        // hold every decoded thumbnail twice for as long as the card lives.
        otherView.ItemsSource = null;
        otherView.Visibility = Visibility.Collapsed;

        if (_showDetails && views.Count > 0)
        {
            detailView.ItemsSource = views;
            detailView.Visibility = Visibility.Visible;
            StackView.Visibility = Visibility.Collapsed;
            ClearStack();
        }
        else
        {
            detailView.ItemsSource = null;
            detailView.Visibility = Visibility.Collapsed;
            StackView.Visibility = views.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            BuildStack(views);
        }

        SummaryChevron.Data = (Geometry)FindResource(_showDetails ? "IconChevronDown" : "IconChevronRight");
    }

    // ---------------- The fanned stack ----------------

    /// <summary>
    /// How the sheets sit for each possible count, back to front. The last entry of each row is
    /// the one on top, and it is always upright and near the middle — the fan reads as a pile
    /// someone set down, and a pile has a front.
    ///
    /// Laid out per count rather than as fixed slots because a fan built for four and then given
    /// two leaves a lopsided gap where the missing sheets were.
    /// </summary>
    private static readonly (double Angle, double OffsetX)[][] FanLayouts =
    [
        [],
        [(0, 0)],
        [(-9, -26), (4, 8)],
        [(-13, -40), (13, 40), (0, 0)],
        [(-16, -50), (16, 50), (-6, -18), (2, 8)]
    ];

    /// <summary>More than this and the extra sheets would be hidden behind the fan anyway; the pill carries the real count.</summary>
    private const int MaxStackSheets = 4;

    /// <summary>
    /// One sheet's visuals plus the transforms that move it, kept for the life of the window.
    ///
    /// Kept rather than rebuilt because animating means the transform has to be the same object
    /// from one layout to the next: hand a slot a fresh TransformGroup each time and there is
    /// nothing for an animation to move *from*, which is exactly how the fan used to snap into
    /// its new shape between one frame and the next.
    /// </summary>
    private sealed class StackSheet
    {
        public required Border Card { get; init; }
        public required Image Icon { get; init; }
        public required Image Thumb { get; init; }
        public required ScaleTransform Scale { get; init; }
        public required RotateTransform Rotate { get; init; }
        public required TranslateTransform Translate { get; init; }

        /// <summary>Whether this sheet was on screen before the current rebuild — the difference between reshaping the fan and a sheet arriving.</summary>
        public bool WasVisible { get; set; }
    }

    private StackSheet[]? _stackSheets;

    private StackSheet[] StackSheets => _stackSheets ??=
    [
        CreateSheet(StackCard0, StackIcon0, StackThumb0),
        CreateSheet(StackCard1, StackIcon1, StackThumb1),
        CreateSheet(StackCard2, StackIcon2, StackThumb2),
        CreateSheet(StackCard3, StackIcon3, StackThumb3)
    ];

    private static StackSheet CreateSheet(Border card, Image icon, Image thumb)
    {
        // Scale first so the arrival pop grows the sheet about its own middle rather than about
        // wherever the rotation has since carried it.
        var scale = new ScaleTransform(1, 1);
        var rotate = new RotateTransform(0);
        var translate = new TranslateTransform(0, 0);
        card.RenderTransform = new TransformGroup { Children = { scale, rotate, translate } };
        return new StackSheet
        {
            Card = card, Icon = icon, Thumb = thumb,
            Scale = scale, Rotate = rotate, Translate = translate
        };
    }

    private static readonly Duration SettleDuration = new(TimeSpan.FromMilliseconds(220));
    private static readonly Duration ArriveDuration = new(TimeSpan.FromMilliseconds(260));

    private void BuildStack(List<ShelfItemView> views)
    {
        var shown = Math.Min(views.Count, MaxStackSheets);

        for (var slot = 0; slot < MaxStackSheets; slot++)
        {
            var sheet = StackSheets[slot];
            if (slot >= shown)
            {
                HideSheet(sheet);
                continue;
            }

            // Back to front: the newest arrival ends up on top, which is what the eye goes to and
            // what the user just dropped.
            var view = views[views.Count - shown + slot];
            var (angle, offsetX) = FanLayouts[shown][slot];

            sheet.Card.ToolTip = view.ToolTipText;
            sheet.Thumb.Source = view.Thumbnail;
            sheet.Thumb.Visibility = view.Thumbnail is not null ? Visibility.Visible : Visibility.Collapsed;
            sheet.Icon.Source = view.Thumbnail is null ? view.Icon : null;
            sheet.Icon.Visibility = view.Thumbnail is null && view.Icon is not null
                ? Visibility.Visible
                : Visibility.Collapsed;
            sheet.Card.Visibility = Visibility.Visible;

            if (sheet.WasVisible)
            {
                // Already on the pile: slide and turn to make room for what just landed, rather
                // than being somewhere else on the next frame.
                Settle(sheet.Rotate, RotateTransform.AngleProperty, angle);
                Settle(sheet.Translate, TranslateTransform.XProperty, offsetX);
            }
            else
            {
                Arrive(sheet, angle, offsetX);
            }

            sheet.WasVisible = true;
        }
    }

    /// <summary>Moves one transform value to its new place, or assigns it outright when it is already there — a reload that changed nothing should not start eight animations.</summary>
    private static void Settle(Animatable transform, DependencyProperty property, double target)
    {
        if (Math.Abs((double)transform.GetValue(property) - target) < 0.01)
        {
            return;
        }
        transform.BeginAnimation(property, new DoubleAnimation(target, SettleDuration)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }

    /// <summary>
    /// A sheet joining the pile: it comes in small and turned further than it will end up, then
    /// settles. This is the whole reason the stack is worth drawing — the shelf's job is to tell
    /// you a file landed, and a sheet that was simply already there on the next frame does not.
    /// </summary>
    private static void Arrive(StackSheet sheet, double angle, double offsetX)
    {
        // Starting position assigned rather than animated, so there is nothing to move from.
        sheet.Rotate.BeginAnimation(RotateTransform.AngleProperty, null);
        sheet.Translate.BeginAnimation(TranslateTransform.XProperty, null);
        sheet.Rotate.Angle = angle;
        sheet.Translate.X = offsetX;

        var pop = new DoubleAnimation(0.55, 1, ArriveDuration)
        {
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 }
        };
        sheet.Scale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
        sheet.Scale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
        sheet.Card.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(150))));
    }

    private static void HideSheet(StackSheet sheet)
    {
        sheet.Card.BeginAnimation(OpacityProperty, null);
        sheet.Scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        sheet.Scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        sheet.Card.Opacity = 1;
        sheet.Scale.ScaleX = 1;
        sheet.Scale.ScaleY = 1;

        sheet.Card.Visibility = Visibility.Collapsed;
        sheet.Card.ToolTip = null;
        // Dropped rather than merely hidden, so a shelf that has been emptied is not still
        // holding every decoded preview it used to show.
        sheet.Icon.Source = null;
        sheet.Icon.Visibility = Visibility.Collapsed;
        sheet.Thumb.Source = null;
        sheet.Thumb.Visibility = Visibility.Collapsed;
        sheet.WasVisible = false;
    }

    private void ClearStack()
    {
        foreach (var sheet in StackSheets)
        {
            HideSheet(sheet);
        }
    }

    private void OnStackPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        _dropLandedOnShelf = false;
    }

    /// <summary>
    /// Dragging the stack takes the whole shelf, which is the gesture the pile is drawn to invite:
    /// the point of collecting five files in one place is to move five files in one motion. The
    /// detail list is still where a single item can be picked out on its own.
    /// </summary>
    private async void OnStackMouseMove(object sender, MouseEventArgs e)
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

        var data = BuildStackDragData();
        if (data is null)
        {
            return;
        }

        // Captured before the drag: ReloadAsync may replace _currentViews while it is in progress.
        var draggedIds = _currentViews.Select(v => v.Model.Id).ToList();
        var effect = DragDrop.DoDragDrop(StackView, data, DragDropEffects.Copy | DragDropEffects.Move);
        await CompleteDragOutAsync(effect, draggedIds);
    }

    /// <summary>Every file the shelf holds as one FileDrop, or every text item as one block when it holds no files at all.</summary>
    private System.Windows.DataObject? BuildStackDragData()
    {
        if (_currentViews.Count == 0)
        {
            return null;
        }

        var paths = _currentViews
            .Where(v => v.Model.Type == ShelfItemType.File && v.Model.FilePath is not null)
            .Select(v => v.Model.FilePath!)
            .ToArray();

        var data = paths.Length > 0
            ? new System.Windows.DataObject(DataFormats.FileDrop, paths)
            : BuildTextDragData();
        if (data is null)
        {
            return null;
        }

        // Same marker a single item carries, so dropping the pile onto one of this card's own
        // action tiles acts on exactly these items rather than collecting them a second time.
        data.SetData(ShelfItemIdsFormat, string.Join(',', _currentViews.Select(v => v.Model.Id)));
        return data;
    }

    private System.Windows.DataObject? BuildTextDragData()
    {
        var text = _currentViews
            .Where(v => v.Model.TextContent is not null)
            .Select(v => v.Model.TextContent!)
            .ToList();
        return text.Count > 0
            ? new System.Windows.DataObject(DataFormats.UnicodeText, string.Join(Environment.NewLine, text))
            : null;
    }

    /// <summary>
    /// Opens the detail list without a click, for the screenshot harness. The stack is the default
    /// view now, so without this the grid and list layouts — and the thumbnail decode behind them
    /// — would go unrendered on every CI run.
    /// </summary>
    internal Task ShowDetailsAsync()
    {
        _showDetails = true;
        return ReloadAsync();
    }

    /// <summary>
    /// Opens the action panel and the delete question without a drag, for the screenshot harness.
    /// Both are states you can only otherwise reach by holding a file over the card, which no CI
    /// run can do — so without these they would ship unrendered every time.
    /// </summary>
    internal void ShowActionsForCapture() => ExpandActions();

    internal void ShowDeleteConfirmForCapture(int itemCount) => _ = AskHowToDeleteAsync(itemCount);

    private async void OnToggleDetailsClicked(object sender, RoutedEventArgs e)
    {
        _showDetails = !_showDetails;
        try
        {
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            CrashLog.Write("Bubble toggle details", ex);
        }
        RestartAutoHideTimer();
    }

    /// <summary>Puts the card away without touching what it holds — the same thing clicking outside does, for people who would rather press a button.</summary>
    private void OnCloseClicked(object sender, RoutedEventArgs e) => HideWithAnimation();

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
        try
        {
            // Only an empty shelf goes away on its own; one holding something is still wanted.
            if (await ReloadAsync() == 0 && !_suppressDeactivateHide)
            {
                HideWithAnimation();
            }
        }
        catch (Exception ex)
        {
            CrashLog.Write("Bubble auto-hide", ex);
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
            HideWithAnimation();
        }
    }

    // ---------------- Taking drops ----------------

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        SetDropEffect(e);
        RootBackground.BorderBrush = (Brush)FindResource("AccentBrush");
    }

    private void OnDragOver(object sender, DragEventArgs e) => SetDropEffect(e);

    /// <summary>
    /// A drag leaving the card is reported here, but so is every hop from one child to the next,
    /// because drag events bubble. The two are told apart by waiting: a hop is followed within
    /// milliseconds by the next DragOver, which cancels the timer. Nothing follows a real
    /// departure, so the timer runs out and the panel folds away.
    /// </summary>
    private void OnDragLeave(object sender, DragEventArgs e)
    {
        RootBackground.BorderBrush = (Brush)FindResource("SurfaceBorderBrush");
        _dragGoneTimer.Stop();
        _dragGoneTimer.Start();
    }

    /// <summary>
    /// Whether the drag pointer is inside an element right now.
    ///
    /// Only ever asked during DragOver. That restriction is the whole point: OLE's DragLeave
    /// carries no coordinates at all — IDropTarget::DragLeave takes no arguments — so a position
    /// read from a leave event is not a position, and a bounds check built on one decides nothing.
    /// The first attempt at this bug did exactly that and the flicker survived it.
    /// </summary>
    private static bool PointerInside(DragEventArgs e, FrameworkElement element)
    {
        if (element.Visibility != Visibility.Visible || element.ActualWidth <= 0)
        {
            return false;
        }
        var point = e.GetPosition(element);
        return point.X >= 0 && point.Y >= 0
               && point.X <= element.ActualWidth && point.Y <= element.ActualHeight;
    }

    private static void SetDropEffect(DragEventArgs e)
    {
        // An item from this shelf hovering over this shelf has nowhere to go, so the cursor should
        // say so rather than promising a drop that the handler will decline.
        var fromThisShelf = e.Data.GetDataPresent(ShelfItemIdsFormat);
        e.Effects = !fromThisShelf && ShelfDropReader.CanRead(e.Data)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        _dropLandedOnShelf = true;
        _dragGoneTimer.Stop();
        CollapseActions();
        RootBackground.BorderBrush = (Brush)FindResource("SurfaceBorderBrush");

        // Dropped back onto the shelf it came from. Nothing to add — it is already here — and
        // adding it would make a duplicate that the drag-out cleanup then removes the original of.
        if (e.Data.GetDataPresent(ShelfItemIdsFormat))
        {
            return;
        }

        // Read the payload before the first await: it belongs to the OLE drag loop, which lets go
        // the moment this handler yields, and reading it afterwards is how a drop that visibly
        // landed ends up adding nothing.
        var dropped = ShelfDropReader.Read(e.Data, shelfId: 0, firstSortOrder: 0);
        if (dropped.Count == 0)
        {
            return;
        }

        // Nothing past this point is allowed to take the app down with it. These handlers are
        // `async void`, so once the first await has run there is no caller left to catch anything
        // they throw — it goes straight to the dispatcher, and a drop that fails on one awkward
        // file used to end the session and the shelf along with it.
        try
        {
            await AddToShelfAsync(dropped);
        }
        catch (Exception ex)
        {
            CrashLog.Write("Bubble drop", ex);
        }
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

    private void OnItemPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        _dropLandedOnShelf = false;
    }

    private void OnItemMouseUp(object sender, MouseButtonEventArgs e) => _dragStartPoint = null;

    private async void OnItemMouseMove(object sender, MouseEventArgs e)
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
        if (data is null)
        {
            return;
        }

        // Whichever of the two views the drag started in is the drag source.
        var dragSource = sender as DependencyObject ?? ItemsList;
        var effect = DragDrop.DoDragDrop(dragSource, data, DragDropEffects.Copy | DragDropEffects.Move);
        await CompleteDragOutAsync(effect, [view.Model.Id]);
    }

    /// <summary>
    /// An item that has been dragged somewhere comes off the shelf.
    ///
    /// The shelf is a staging area, not storage: you put things on it in order to take them
    /// somewhere, and once a file has gone where it was going, leaving a copy of it here means the
    /// next collection starts among the leftovers of the last one. Clearing by hand every time is
    /// work the gesture already told us was unnecessary.
    ///
    /// This removes the item from the shelf, never from the disk. The file stays wherever it was
    /// and now also wherever it was dropped.
    /// </summary>
    private async Task CompleteDragOutAsync(DragDropEffects effect, IReadOnlyList<long> itemIds)
    {
        // None means nothing accepted the drop — Escape, or a target that refused it. The item was
        // never delivered anywhere, so taking it off the shelf would simply lose it.
        if (effect == DragDropEffects.None || _dropLandedOnShelf || itemIds.Count == 0)
        {
            return;
        }

        try
        {
            foreach (var id in itemIds)
            {
                await _app.ShelfSession.RemoveItemAsync(id);
            }
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            CrashLog.Write("Bubble drag out", ex);
        }
        RestartAutoHideTimer();
    }

    /// <summary>
    /// Set by this card's own drop handlers, and read once each drag-out finishes.
    ///
    /// A drag that ends on one of the action tiles, or back on the card itself, has not taken the
    /// item anywhere — so it must not count as delivered. Without this, dropping onto "Sao chép"
    /// would copy the file to a folder and then empty the shelf, which is the one thing copying
    /// is supposed not to do.
    /// </summary>
    private bool _dropLandedOnShelf;

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
    /// <summary>
    /// Opening the actions is what a drag arriving over the button means — there is no other
    /// reason to be there holding a file.
    /// </summary>
    private void OnActionToggleDragEnter(object sender, DragEventArgs e) => SetActionDropEffect(sender, e);

    /// <summary>
    /// Folds the actions away when the pointer leaves the whole panel.
    ///
    /// The bounds check is the point: WPF raises DragLeave on the panel every time the pointer
    /// crosses from it onto one of its own tiles, because the event bubbles up from the child.
    /// Acting on those would close the panel at the exact moment the user aimed at something.
    /// </summary>
    /// <summary>
    /// Opens and closes the action panel from where the pointer actually is.
    ///
    /// Tunnelling, and on the window, so it runs before any child marks the event handled — the
    /// tiles and the card all set Handled on DragOver to claim the drop effect, which would
    /// otherwise stop a bubbling handler here from ever seeing the move.
    ///
    /// Everything about opening and closing is decided here, from one event that carries a real
    /// position and fires on every move. The previous version decided it from DragEnter and
    /// DragLeave, and those fire on every hop between children as well — which is how opening the
    /// panel over the toggle immediately looked like leaving the toggle, and closed it again.
    /// </summary>
    private void OnPreviewDragOver(object sender, DragEventArgs e)
    {
        _dragGoneTimer.Stop();

        if (!ShelfDropReader.CanRead(e.Data) && !e.Data.GetDataPresent(ShelfItemIdsFormat))
        {
            return;
        }

        if (ActionOverlay.Visibility == Visibility.Visible)
        {
            if (!PointerInside(e, ActionOverlay))
            {
                CollapseActions();
            }
        }
        else if (PointerInside(e, ActionToggle))
        {
            ExpandActions();
        }
    }

    /// <summary>Clicking opens the same panel, for running an action on the whole shelf without a drag.</summary>
    private void OnActionToggleClicked(object sender, RoutedEventArgs e)
    {
        if (ActionOverlay.Visibility == Visibility.Visible)
        {
            CollapseActions();
        }
        else
        {
            ExpandActions();
        }
    }

    private void ExpandActions()
    {
        // Already open: opening again would restart the idle timer on every mouse move, and a
        // panel that is repeatedly told to appear is how the last flicker started.
        if (ActionOverlay.Visibility == Visibility.Visible)
        {
            return;
        }

        ActionOverlay.Visibility = Visibility.Visible;
        ActionToggle.Visibility = Visibility.Hidden;
        RestartAutoHideTimer();
    }

    private void CollapseActions()
    {
        ActionOverlay.Visibility = Visibility.Collapsed;
        ActionToggle.Visibility = Visibility.Visible;
    }

    private async void OnActionDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        _dropLandedOnShelf = true;
        _dragGoneTimer.Stop();
        CollapseActions();
        if (sender is not Button { CommandParameter: QuickActionType action } button)
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

        try
        {
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
        catch (Exception ex)
        {
            CrashLog.Write("Bubble action drop", ex);
        }
    }

    private async void OnActionClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: QuickActionType action })
        {
            return;
        }
        try
        {
            await RunActionAsync(action, await _app.ShelfSession.GetItemsAsync(_shelfId));
        }
        catch (Exception ex)
        {
            CrashLog.Write("Bubble action click", ex);
        }
    }

    private async Task RunActionAsync(QuickActionType action, IReadOnlyList<ShelfItem> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        // The delete tile arrives here carrying the recoverable action as a placeholder; which
        // delete actually runs is the answer to the question below, and "no answer" is a valid
        // one that ends this here.
        if (IsDelete(action))
        {
            var chosen = await AskHowToDeleteAsync(items.Count);
            if (chosen is null)
            {
                return;
            }
            action = chosen.Value;
        }

        var targetPath = await ResolveTargetPathAsync(action);
        if (targetPath is null && NeedsTargetFolder(action))
        {
            return;   // Folder picker cancelled; the shelf keeps everything.
        }

        var result = await _app.QuickActions.ExecuteOnItemsAsync(items, action, targetPath);

        // A file that has been deleted is not on the shelf either — the row would point at
        // nothing, which is the same reason a moved file comes off.
        if (action == QuickActionType.MoveToFolder || IsDelete(action))
        {
            foreach (var itemResult in result.ItemResults.Where(r => r.Succeeded))
            {
                await _app.ShelfSession.RemoveItemAsync(itemResult.ShelfItemId);
            }
        }

        await ReloadAsync();
        RestartAutoHideTimer();
    }

    private static bool IsDelete(QuickActionType action) =>
        action is QuickActionType.DeleteToRecycleBin or QuickActionType.DeletePermanently;

    /// <summary>The pending delete question, completed by whichever of the three buttons is pressed.</summary>
    private TaskCompletionSource<QuickActionType?>? _deleteChoice;

    /// <summary>
    /// Asks whether to recycle or destroy, and waits for the answer.
    ///
    /// In the card rather than a message box: this question arrives at the end of a drag, over
    /// the user's own work, and a system dialog appearing somewhere else on screen is both uglier
    /// and easier to answer without reading. Cancelling — clicking Huỷ, or the shelf going away —
    /// is a null answer, and a null answer deletes nothing.
    /// </summary>
    private Task<QuickActionType?> AskHowToDeleteAsync(int itemCount)
    {
        _deleteChoice?.TrySetResult(null);

        DeleteConfirmText.Text = itemCount == 1
            ? "Xoá tệp này khỏi ổ đĩa?"
            : $"Xoá {itemCount} tệp khỏi ổ đĩa?";
        DeleteConfirm.Visibility = Visibility.Visible;
        CollapseActions();

        // The card must not put itself away while it is holding a question.
        _autoHideTimer.Stop();
        _suppressDeactivateHide = true;

        _deleteChoice = new TaskCompletionSource<QuickActionType?>(TaskCreationOptions.RunContinuationsAsynchronously);
        return _deleteChoice.Task;
    }

    private void AnswerDelete(QuickActionType? answer)
    {
        DeleteConfirm.Visibility = Visibility.Collapsed;
        _suppressDeactivateHide = false;
        RestartAutoHideTimer();

        var pending = _deleteChoice;
        _deleteChoice = null;
        pending?.TrySetResult(answer);
    }

    private void OnDeleteToRecycleBinClicked(object sender, RoutedEventArgs e) =>
        AnswerDelete(QuickActionType.DeleteToRecycleBin);

    private void OnDeletePermanentlyClicked(object sender, RoutedEventArgs e) =>
        AnswerDelete(QuickActionType.DeletePermanently);

    private void OnDeleteCancelClicked(object sender, RoutedEventArgs e) => AnswerDelete(null);

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

    private async void OnRemoveItemClicked(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not FrameworkElement { DataContext: ShelfItemView view })
        {
            return;
        }

        try
        {
            await _app.ShelfSession.RemoveItemAsync(view.Model.Id);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            CrashLog.Write("Bubble remove item", ex);
        }
        RestartAutoHideTimer();
    }

    /// <summary>
    /// Empties the shelf. No confirmation: this removes items from a shelf, it does not touch a
    /// single file on disk, and the shelf is a scratch surface — asking "are you sure" every time
    /// someone clears their scratch surface is how a confirmation dialog becomes something people
    /// dismiss without reading.
    /// </summary>
    private async void OnClearClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            await _app.ShelfSession.ClearItemsAsync(_shelfId);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            CrashLog.Write("Bubble clear", ex);
        }
        RestartAutoHideTimer();
    }
}

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace WinClipboard.App.Services;

/// <summary>
/// Makes the tray menu look like the rest of the application instead of like Windows 2005.
///
/// A default ContextMenuStrip is grey, sharp-cornered, and carries a raised vertical gutter down
/// the left where icons would go — a strip this menu has no icons for, which leaves the labels
/// pushed away from an edge that exists for nothing. Next to a dark glass card with rounded
/// corners it reads as a piece of a different program.
///
/// Every colour here is read from the application's own resource dictionary rather than typed in.
/// The first version of this file did type them in, and they were all slightly wrong — the
/// surface two steps off the card's, the border darker and bluer than the card's — because
/// hand-copied hex has no way of staying in step with a palette that moves. Reading the brushes
/// means the menu cannot drift from the card again.
/// </summary>
internal static class TrayMenuTheme
{
    /// <summary>Matches the shelf card, which is the other thing in this app that floats over the desktop.</summary>
    private const int PopupCornerRadius = 12;

    /// <summary>The radius every small control in Theme.xaml uses.</summary>
    private const int ItemCornerRadius = 6;

    private static readonly Color Surface = Opaque("GlassBrush", Color.FromArgb(28, 28, 32));
    private static readonly Color SurfaceBorder = Over("GlassBorderBrush", Surface, Color.FromArgb(87, 87, 89));
    private static readonly Color Separator = Over("SurfaceBorderBrush", Surface, Color.FromArgb(61, 61, 64));
    private static readonly Color Highlight = Opaque("AccentBrush", Color.FromArgb(59, 130, 246));
    private static readonly Color TextPrimary = Opaque("TextPrimaryBrush", Color.FromArgb(242, 242, 242));
    private static readonly Color TextMuted = Over("TextMutedBrush", Surface, Color.FromArgb(122, 122, 125));

    public static void Apply(ContextMenuStrip menu)
    {
        menu.Renderer = new Renderer();
        menu.BackColor = Surface;
        menu.ForeColor = TextPrimary;
        menu.ShowImageMargin = false;
        menu.DropShadowEnabled = true;
        menu.Padding = new Padding(4);
        // 9pt is the app's 12 device-independent units, which is what its own list rows use.
        menu.Font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);

        foreach (ToolStripItem item in menu.Items)
        {
            if (item is not ToolStripMenuItem entry)
            {
                continue;
            }
            entry.Padding = new Padding(8, 5, 8, 5);
            entry.ForeColor = TextPrimary;
            MoveShortcutOutOfTheLabel(entry);
        }

        // The region has to be reset every time the popup opens: WinForms rebuilds the handle
        // between showings, and a region set once is gone by the second right-click.
        menu.Opened += (_, _) => RoundCorners(menu);
    }

    /// <summary>
    /// "Mở lịch sử clipboard\tCtrl+Shift+V" was one string with a tab in it, which renders as a
    /// gap of whatever width the tab stop happens to fall on. Handing the shortcut to WinForms as
    /// a shortcut instead gets it right-aligned in its own column — and lets the renderer draw it
    /// muted, the way every other secondary line in this app is drawn.
    /// </summary>
    private static void MoveShortcutOutOfTheLabel(ToolStripMenuItem entry)
    {
        var tab = entry.Text?.IndexOf('\t') ?? -1;
        if (tab < 0)
        {
            return;
        }

        var label = entry.Text![..tab];
        var shortcut = entry.Text[(tab + 1)..];
        entry.Text = label;
        entry.ShortcutKeyDisplayString = shortcut;
        entry.ShowShortcutKeys = true;
    }

    private static void RoundCorners(ContextMenuStrip menu)
    {
        try
        {
            using var path = RoundedPath(new Rectangle(0, 0, menu.Width - 1, menu.Height - 1), PopupCornerRadius);
            menu.Region = new Region(path);
        }
        catch
        {
            // A square menu is a cosmetic loss; a menu that throws while opening is the only way
            // out of the application becoming unreachable.
        }
    }

    private static GraphicsPath RoundedPath(Rectangle bounds, int radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    // ---------------- Reading the app's palette ----------------

    private static System.Windows.Media.Color? Lookup(string resourceKey)
    {
        try
        {
            return (Application.Current?.TryFindResource(resourceKey) as System.Windows.Media.SolidColorBrush)?.Color;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>A theme colour with its alpha dropped — for brushes the app paints over a backdrop that the menu does not have.</summary>
    private static Color Opaque(string resourceKey, Color fallback) =>
        Lookup(resourceKey) is { } c ? Color.FromArgb(c.R, c.G, c.B) : fallback;

    /// <summary>
    /// A translucent theme colour flattened onto the surface beneath it.
    ///
    /// The app's borders and secondary text are white at low alpha, which works because WPF
    /// composites them over whatever the card is showing. GDI+ menu painting has no such
    /// backdrop, so the blend has to be done here — otherwise a 15%-alpha white border is drawn
    /// as very nearly white.
    /// </summary>
    private static Color Over(string resourceKey, Color under, Color fallback)
    {
        if (Lookup(resourceKey) is not { } c)
        {
            return fallback;
        }

        var alpha = c.A / 255.0;
        return Color.FromArgb(
            (int)Math.Round(under.R + (c.R - under.R) * alpha),
            (int)Math.Round(under.G + (c.G - under.G) * alpha),
            (int)Math.Round(under.B + (c.B - under.B) * alpha));
    }

    private sealed class Renderer : ToolStripProfessionalRenderer
    {
        public Renderer() : base(new Colours())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            // The shortcut arrives through this same call, as a second draw with its own text.
            var isShortcut = e.Item is ToolStripMenuItem { ShortcutKeyDisplayString: { } s }
                             && s.Length > 0 && e.Text == s;

            e.TextColor = isShortcut
                ? (e.Item.Selected ? Color.FromArgb(220, 235, 255) : TextMuted)
                : (e.Item.Selected ? Color.White : TextPrimary);
            base.OnRenderItemText(e);
        }

        /// <summary>A single hairline across the menu rather than the default etched pair of lines.</summary>
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using var pen = new Pen(Separator);
            var y = e.Item.Height / 2;
            e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected)
            {
                return;
            }

            // Inset and rounded, so the highlight reads as a pill behind the label rather than a
            // band painted edge to edge across the popup.
            var bounds = new Rectangle(3, 1, e.Item.Width - 6, e.Item.Height - 2);
            using var brush = new SolidBrush(Highlight);
            using var path = RoundedPath(bounds, ItemCornerRadius);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillPath(brush, path);
            e.Graphics.SmoothingMode = SmoothingMode.Default;
        }

        private sealed class Colours : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground => Surface;
            public override Color MenuBorder => SurfaceBorder;
            public override Color MenuItemBorder => Highlight;
            public override Color MenuItemSelected => Highlight;
            public override Color ImageMarginGradientBegin => Surface;
            public override Color ImageMarginGradientMiddle => Surface;
            public override Color ImageMarginGradientEnd => Surface;
            public override Color SeparatorDark => Separator;
            public override Color SeparatorLight => Separator;
        }
    }
}

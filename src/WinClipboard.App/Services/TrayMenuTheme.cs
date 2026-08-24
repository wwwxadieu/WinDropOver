using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WinClipboard.App.Services;

/// <summary>
/// Makes the tray menu look like the rest of the application instead of like Windows 2005.
///
/// A default ContextMenuStrip is grey, sharp-cornered, and carries a raised vertical gutter down
/// the left where icons would go — a strip this menu has no icons for, which leaves the labels
/// pushed away from an edge that exists for nothing. Next to a dark glass card with rounded
/// corners it reads as a piece of a different program.
///
/// WinForms will not restyle any of that on its own, but every part of it is a renderer decision,
/// and a renderer is replaceable. This one paints a dark rounded surface, drops the gutter, and
/// gives the highlight the app's own accent.
/// </summary>
internal static class TrayMenuTheme
{
    private static readonly Color Surface = Color.FromArgb(30, 30, 34);
    private static readonly Color SurfaceBorder = Color.FromArgb(64, 64, 70);
    private static readonly Color Highlight = Color.FromArgb(59, 130, 246);
    private static readonly Color TextPrimary = Color.FromArgb(242, 242, 242);
    private static readonly Color Separator = Color.FromArgb(58, 58, 64);

    /// <summary>Rounded corners on the popup itself, so the dark fill does not sit inside a square grey frame.</summary>
    private const int CornerRadius = 8;

    public static void Apply(ContextMenuStrip menu)
    {
        menu.Renderer = new Renderer();
        menu.BackColor = Surface;
        menu.ForeColor = TextPrimary;
        menu.ShowImageMargin = false;
        menu.DropShadowEnabled = true;
        menu.Padding = new Padding(4);
        menu.Font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);

        foreach (ToolStripItem item in menu.Items)
        {
            if (item is ToolStripMenuItem entry)
            {
                entry.Padding = new Padding(6, 4, 6, 4);
                entry.ForeColor = TextPrimary;
            }
        }

        // The region has to be reset every time the popup opens: WinForms rebuilds the handle
        // between showings, and a region set once is gone by the second right-click.
        menu.Opened += (_, _) => RoundCorners(menu);
    }

    private static void RoundCorners(ContextMenuStrip menu)
    {
        try
        {
            using var path = new GraphicsPath();
            var w = menu.Width;
            var h = menu.Height;
            var d = CornerRadius * 2;
            path.AddArc(0, 0, d, d, 180, 90);
            path.AddArc(w - d - 1, 0, d, d, 270, 90);
            path.AddArc(w - d - 1, h - d - 1, d, d, 0, 90);
            path.AddArc(0, h - d - 1, d, d, 90, 90);
            path.CloseFigure();
            menu.Region = new Region(path);
        }
        catch
        {
            // A square menu is a cosmetic loss; a menu that throws while opening is the only way
            // out of the application becoming unreachable.
        }
    }

    private sealed class Renderer : ToolStripProfessionalRenderer
    {
        public Renderer() : base(new Colours())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Selected ? Color.White : TextPrimary;
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
            using var path = new GraphicsPath();
            const int radius = 5;
            path.AddArc(bounds.X, bounds.Y, radius * 2, radius * 2, 180, 90);
            path.AddArc(bounds.Right - radius * 2, bounds.Y, radius * 2, radius * 2, 270, 90);
            path.AddArc(bounds.Right - radius * 2, bounds.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();

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

using System.Drawing;
using System.Windows.Forms;

namespace WinClipboard.App.Services;

/// <summary>System tray icon + right-click menu (plan 3.2 / section 4 "Tray & Cài đặt").</summary>
public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;

    public event EventHandler? OpenShelfRequested;
    public event EventHandler? OpenHistoryRequested;
    public event EventHandler? OpenSettingsRequested;
    public event EventHandler? ExitRequested;

    public TrayIconService()
    {
        var menu = new ContextMenuStrip();
        // Dropover can raise its shelf from the menu bar as well as by gesture/shortcut; without
        // this the shelf was only reachable mid-drag, so there was no way to look at what it held.
        var openShelfItem = new ToolStripMenuItem("Mở shelf");
        openShelfItem.Click += (_, _) => OpenShelfRequested?.Invoke(this, EventArgs.Empty);
        var openHistoryItem = new ToolStripMenuItem("Mở lịch sử clipboard\tCtrl+Shift+V");
        openHistoryItem.Click += (_, _) => OpenHistoryRequested?.Invoke(this, EventArgs.Empty);
        var settingsItem = new ToolStripMenuItem("Cài đặt...");
        settingsItem.Click += (_, _) => OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
        var exitItem = new ToolStripMenuItem("Thoát");
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        menu.Items.Add(openShelfItem);
        menu.Items.Add(openHistoryItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(settingsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadAppIcon(),
            Text = "WinClipboard",
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => OpenHistoryRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Draws a simple filled-circle icon at runtime instead of shipping an .ico asset — swap in
    /// a real app icon under Assets/ and point NotifyIcon.Icon at it via
    /// new Icon("Assets/app.ico") once branding (plan section 7) is decided.
    /// </summary>
    /// <summary>
    /// The application's own icon, from the resource compiled into the executable.
    ///
    /// Falls back to drawing a plain disc if that lookup fails, because a tray icon is how this
    /// application is reached at all — quitting it, opening settings, opening the shelf — and an
    /// exception here would leave a running process with no way to get at any of that.
    /// </summary>
    private static Icon LoadAppIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/WinClipboard.ico", UriKind.Absolute);
            using var stream = System.Windows.Application.GetResourceStream(uri)!.Stream;
            // 32px: the size Windows asks for in the notification area at 200% scaling, and the
            // .ico carries an exact match so nothing is resampled.
            return new Icon(stream, 32, 32);
        }
        catch
        {
            return CreateFallbackIcon();
        }
    }

    private static Icon CreateFallbackIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(Color.FromArgb(255, 0x3B, 0x82, 0xF6));
            g.FillEllipse(brush, 2, 2, 28, 28);
        }
        // GetHicon()'s handle is technically owned by the caller (should go through DestroyIcon),
        // but this runs once for the process's lifetime, so the leak is bounded and freed at exit.
        var handle = bitmap.GetHicon();
        return Icon.FromHandle(handle);
    }

    /// <summary>
    /// Balloon notification from the tray. Used when an action fails but the app carries on —
    /// without it a drop that threw would simply appear to do nothing, which is indistinguishable
    /// from the app ignoring the user.
    /// </summary>
    public void ShowWarning(string title, string message)
    {
        try
        {
            _notifyIcon.ShowBalloonTip(6000, title, message, ToolTipIcon.Warning);
        }
        catch
        {
            // A notification that cannot be shown is not worth a second failure.
        }
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}

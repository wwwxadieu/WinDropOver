using System.Drawing;
using System.Windows.Forms;

namespace WinClipboard.App.Services;

/// <summary>System tray icon + right-click menu (plan 3.2 / section 4 "Tray & Cài đặt").</summary>
public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;

    public event EventHandler? OpenHistoryRequested;
    public event EventHandler? OpenSettingsRequested;
    public event EventHandler? ExitRequested;

    public TrayIconService()
    {
        var menu = new ContextMenuStrip();
        var openHistoryItem = new ToolStripMenuItem("Mở lịch sử clipboard\tCtrl+Shift+V");
        openHistoryItem.Click += (_, _) => OpenHistoryRequested?.Invoke(this, EventArgs.Empty);
        var settingsItem = new ToolStripMenuItem("Cài đặt...");
        settingsItem.Click += (_, _) => OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
        var exitItem = new ToolStripMenuItem("Thoát");
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        menu.Items.Add(openHistoryItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = CreateFallbackIcon(),
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

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}

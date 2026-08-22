namespace WinClipboard.Core.Models;

/// <summary>Virtual-screen or single-monitor bounds in device pixels. Avoids taking a System.Drawing dependency for what is otherwise plain arithmetic.</summary>
public readonly record struct ScreenRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;

    public bool Contains(int x, int y) => x >= Left && x <= Right && y >= Top && y <= Bottom;
}

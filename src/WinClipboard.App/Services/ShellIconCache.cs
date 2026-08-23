using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinClipboard.Interop;

namespace WinClipboard.App.Services;

/// <summary>
/// Turns a file path into the icon Explorer shows for it, cached.
///
/// Cached because the shelf rebuilds its whole item list on every reload — every drop, every
/// action, every auto-hide tick — and asking the shell for an icon is a real round trip that can
/// touch the disk. Twenty items reloading a few times a second would be doing that work
/// continuously for an answer that almost never changes.
///
/// Most files share an icon with every other file of the same extension, so that is the cache
/// key. The exceptions are the formats that carry their own artwork: an executable's icon is the
/// application's, and no two are alike. Those are keyed by path instead.
/// </summary>
internal static class ShellIconCache
{
    private static readonly ConcurrentDictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Extensions where the icon belongs to the individual file rather than to the file type.</summary>
    private static readonly HashSet<string> PerFileIconExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".lnk", ".ico", ".msi", ".cpl", ".scr", ".msc", ".url"
    };

    /// <summary>
    /// Null when the path has no icon to give — a missing file, or a shell that declined.
    ///
    /// A hit returns without yielding, which is the common case by a wide margin: the shelf
    /// reloads constantly and its items rarely change type. Only a miss goes to a worker, because
    /// only a miss asks the shell anything, and that can reach the disk — including a disk that
    /// has gone away, on a path that came off a network share or a drive since unplugged.
    /// </summary>
    public static Task<ImageSource?> GetAsync(string path)
    {
        string key;
        try
        {
            var extension = Path.GetExtension(path);
            // Directories have no extension and an icon of their own, so they cannot share the
            // "no extension" bucket with extensionless files.
            key = string.IsNullOrEmpty(extension) || PerFileIconExtensions.Contains(extension)
                ? path
                : extension;
        }
        catch
        {
            return Task.FromResult<ImageSource?>(null);
        }

        return Cache.TryGetValue(key, out var cached)
            ? Task.FromResult(cached)
            : Task.Run(() => Cache.GetOrAdd(key, _ => Load(path)));
    }

    private static ImageSource? Load(string path)
    {
        var handle = ShellIcon.Load(path);
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(
                handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            // Frozen so it can be built off the UI thread and shared by every item that keys to it.
            source.Freeze();
            return source;
        }
        catch
        {
            return null;
        }
        finally
        {
            // Unconditional: the bitmap above copies the pixels, so the handle is finished with
            // either way, and an icon handle that escapes here is gone for the life of the
            // process. There are ten thousand of them per process, shared with every window,
            // brush and font the app owns.
            ShellIcon.Destroy(handle);
        }
    }
}

namespace WinClipboard.Core.Utils;

/// <summary>
/// Decides whether a piece of copied or dropped text should be filed as a link rather than plain
/// text. Shared by the clipboard monitor and the shelf drop handler so both classify identically —
/// otherwise the same URL could land in history as a Link and on a shelf as Text.
/// </summary>
public static class LinkDetector
{
    private static readonly string[] LinkSchemes = ["http", "https", "ftp", "ftps", "mailto"];

    public static bool IsLink(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();

        // A URL with whitespace in it is really a sentence that happens to start with one.
        if (trimmed.Any(char.IsWhiteSpace))
        {
            return false;
        }

        return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
               && LinkSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase);
    }
}

using System.Security.Cryptography;
using System.Text;
using WinClipboard.Core.Models;

namespace WinClipboard.Core.Utils;

/// <summary>Computes the dedup hash stored in <see cref="ClipboardItem.HashDedup"/>.</summary>
public static class ContentHasher
{
    public static string HashText(string text) =>
        Hash(ContentType.Text, Encoding.UTF8.GetBytes(text));

    public static string HashBytes(ContentType type, byte[] bytes) =>
        Hash(type, bytes);

    /// <summary>Files/links are deduped by path rather than content, so a re-copy of the same path is recognized without re-reading the file.</summary>
    public static string HashPath(ContentType type, string path) =>
        Hash(type, Encoding.UTF8.GetBytes(path));

    private static string Hash(ContentType type, byte[] payload)
    {
        using var sha256 = SHA256.Create();
        var prefixed = new byte[payload.Length + 1];
        prefixed[0] = (byte)type;
        Buffer.BlockCopy(payload, 0, prefixed, 1, payload.Length);
        var digest = sha256.ComputeHash(prefixed);
        return Convert.ToHexString(digest);
    }
}

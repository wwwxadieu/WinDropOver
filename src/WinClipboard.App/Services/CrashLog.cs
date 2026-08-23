using System.IO;
using System.Text;

namespace WinClipboard.App.Services;

/// <summary>
/// Appends unexpected failures to %LocalAppData%\WinClipboard\crash.log.
///
/// This exists because the app is a GUI-subsystem binary with no console: when it died on a
/// user's machine there was nothing at all to look at, and the only way to work out why was to
/// reason about the source and guess. A log turns the next report from "it closed" into a stack
/// trace.
///
/// Deliberately best-effort and self-silencing — a logger that throws while reporting a crash
/// would replace the diagnostic with a second crash.
/// </summary>
public static class CrashLog
{
    private static readonly object Gate = new();

    public static string Path { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinClipboard", "crash.log");

    public static void Write(string context, Exception? exception)
    {
        try
        {
            var text = new StringBuilder()
                .Append('[').Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz")).Append("] ")
                .AppendLine(context)
                .AppendLine(exception?.ToString() ?? "(no exception object)")
                .AppendLine()
                .ToString();

            lock (Gate)
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
                // Keep the file from growing without bound across a long-lived install.
                if (File.Exists(Path) && new FileInfo(Path).Length > 512 * 1024)
                {
                    File.Move(Path, Path + ".old", overwrite: true);
                }
                File.AppendAllText(Path, text);
            }
        }
        catch
        {
            // Nothing useful can be done if even writing the log fails.
        }
    }
}

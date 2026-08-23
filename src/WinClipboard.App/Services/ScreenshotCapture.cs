using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WinClipboard.App.Views;
using WinClipboard.Core.Models;
using WinClipboard.Core.Utils;

namespace WinClipboard.App.Services;

/// <summary>
/// Renders each of the app's windows to a PNG, driven by `WinClipboard.exe --screenshots &lt;dir&gt;`.
///
/// Two jobs in one: it produces the screenshots the README/release notes need, and it is the
/// only thing in the repo that actually *instantiates* the XAML. A compile only proves the
/// markup parses — it says nothing about a misspelled StaticResource key, a binding to a
/// property that does not exist, or a converter that throws. Those surface here as a failed
/// capture, which is why the CI job treats any exception as a build failure.
/// </summary>
internal static class ScreenshotCapture
{
    private const string Flag = "--screenshots";

    /// <summary>2x so the PNGs stay sharp when viewed on a high-DPI screen or scaled in a README.</summary>
    private const double RenderScale = 2.0;

    private const int AttachParentProcess = -1;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(int dwProcessId);

    public static bool TryParseOutputDirectory(string[] args, out string outputDirectory)
    {
        outputDirectory = string.Empty;
        var index = Array.IndexOf(args, Flag);
        if (index < 0 || index + 1 >= args.Length)
        {
            return false;
        }
        outputDirectory = Path.GetFullPath(args[index + 1]);

        // This is a WinExe (GUI subsystem), so it starts with no console and Console.WriteLine
        // goes nowhere. Borrow the launching shell's console so progress and failures actually
        // land in the CI log.
        AttachConsole(AttachParentProcess);
        return true;
    }

    private static readonly List<string> LogLines = [];

    private static void Log(string message)
    {
        LogLines.Add(message);
        Console.WriteLine(message);
    }

    /// <summary>
    /// Runs the whole capture and returns a process exit code. Everything is also written to
    /// screenshot-log.txt in the output directory: this is a WinExe, so console output is not
    /// reliably visible to whatever launched it, and the log file is what CI actually reads.
    /// </summary>
    private static string? _logPath;

    /// <summary>
    /// Called when a fire-and-forget task faults. In screenshot mode this must reach the log —
    /// the previous CI run died with a bare 0xE0434352 and no log at all, because an async void
    /// method threw where nothing could catch it.
    /// </summary>
    public static void ReportBackgroundFailure(Exception ex)
    {
        if (_logPath is null)
        {
            return; // Not in screenshot mode; a normal run just carries on.
        }
        Log($"BACKGROUND FAILURE: {ex}");
        FlushLog();
    }

    public static async Task<int> RunAsync(App app, string outputDirectory)
    {
        try
        {
            Directory.CreateDirectory(outputDirectory);
            _logPath = Path.Combine(outputDirectory, "screenshot-log.txt");

            // Catch anything that escapes the await chain (a throwing event handler, a faulted
            // async void) so the log always explains a failure instead of the process just dying.
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                Log($"UNHANDLED: {e.ExceptionObject}");
                FlushLog();
            };
            app.DispatcherUnhandledException += (_, e) =>
            {
                Log($"DISPATCHER UNHANDLED: {e.Exception}");
                FlushLog();
            };

            await CaptureAllAsync(app, outputDirectory);
            Log("All screenshots captured successfully.");
            return 0;
        }
        catch (Exception ex)
        {
            Log($"FAILED: {ex}");
            return 1;
        }
        finally
        {
            FlushLog();
        }
    }

    private static void FlushLog()
    {
        if (_logPath is null)
        {
            return;
        }
        try
        {
            File.WriteAllLines(_logPath, LogLines);
        }
        catch (IOException)
        {
            // Losing the log must not mask the real exit code.
        }
    }

    private static async Task CaptureAllAsync(App app, string outputDirectory)
    {
        Log("Seeding sample data...");
        await SeedSampleDataAsync(app);
        Log("Sample data seeded.");

        var opened = new List<Window>();
        var failures = new List<string>();

        // Each window is captured independently: one failing window should still leave the other
        // three usable, and the log then names exactly which one broke.
        BubbleWindow? bubble = null;

        async Task StepAsync(string name, string fileName, Func<Task<Window>> show)
        {
            try
            {
                Log($"--- {name} ---");
                var window = await show();
                opened.Add(window);
                await CaptureAsync(window, Path.Combine(outputDirectory, fileName));
            }
            catch (Exception ex)
            {
                failures.Add(name);
                Log($"{name} FAILED: {ex}");
            }
        }

        await StepAsync("history overlay", "01-history-overlay.png", async () =>
        {
            var window = new HistoryOverlayWindow(app);
            await window.ShowOverlayAsync();
            return window;
        });

        await StepAsync("bubble", "02-bubble.png", async () =>
        {
            var window = new BubbleWindow(app);
            await window.ShowAtEdgeAsync(ScreenEdge.Right);
            bubble = window;
            return window;
        });

        await StepAsync("shelf panel", "03-shelf-panel.png", async () =>
        {
            if (bubble is null)
            {
                throw new InvalidOperationException("Bubble window failed, so the panel cannot be positioned next to it.");
            }
            var window = app.GetOrCreatePanelWindow();
            await window.ShowNextToAsync(bubble, ScreenEdge.Right);
            return window;
        });

        await StepAsync("settings", "04-settings.png", () =>
        {
            var window = new SettingsWindow(app);
            window.Show();
            return Task.FromResult<Window>(window);
        });

        foreach (var window in opened)
        {
            window.Close();
        }

        if (failures.Count > 0)
        {
            throw new InvalidOperationException($"Failed to capture: {string.Join(", ", failures)}");
        }
    }

    /// <summary>
    /// Populates the shelf and clipboard history so the screenshots show a realistic populated
    /// state rather than four empty panels.
    /// </summary>
    private static async Task SeedSampleDataAsync(App app)
    {
        var now = DateTimeOffset.UtcNow;

        var clipboardSamples = new (ContentType Type, string? Text, string? FilePath, string Source, int MinutesAgo, bool Pinned)[]
        {
            (ContentType.Text, "Báo cáo quý III — bản nháp cuối cùng, gửi anh Minh trước thứ Sáu", null, "WINWORD", 2, true),
            (ContentType.Link, "https://github.com/wwwxadieu/WinDropOver", null, "chrome", 8, false),
            (ContentType.File, null, @"C:\Users\me\Documents\hop-dong-2026.pdf", "explorer", 21, false),
            (ContentType.Text, "0912 345 678", null, "Telegram", 45, false),
            (ContentType.Text, "SELECT * FROM ClipboardItem WHERE IsPinned = 1 ORDER BY CreatedAt DESC", null, "devenv", 96, false),
        };

        foreach (var sample in clipboardSamples)
        {
            await app.ClipboardRepository.AddAsync(new ClipboardItem
            {
                Type = sample.Type,
                CreatedAt = now.AddMinutes(-sample.MinutesAgo),
                TextContent = sample.Text,
                FilePath = sample.FilePath,
                SourceApp = sample.Source,
                IsPinned = sample.Pinned,
                HashDedup = sample.Text is not null
                    ? ContentHasher.HashText(sample.Text)
                    : ContentHasher.HashPath(sample.Type, sample.FilePath!)
            });
        }

        var workShelfId = await app.ShelfSession.EnsureDefaultShelfAsync();
        var workShelf = (await app.ShelfSession.GetShelfAsync(workShelfId))!;
        workShelf.Name = "Công việc";
        await app.ShelfSession.UpdateShelfAsync(workShelf);

        var workFiles = new[] { "bao-cao-q3.docx", "so-lieu.xlsx", "anh-bia.png" };
        for (var i = 0; i < workFiles.Length; i++)
        {
            await app.ShelfSession.AddItemAsync(new ShelfItem
            {
                ShelfId = workShelfId,
                Type = ShelfItemType.File,
                FilePath = $@"C:\Users\me\Documents\{workFiles[i]}",
                AddedAt = now.AddMinutes(-i),
                SortOrder = i
            });
        }

        // A second shelf so the panel's tab strip is visibly doing its job.
        var photoShelfId = await app.ShelfSession.CreateShelfAsync(new Shelf
        {
            Name = "Ảnh gửi khách",
            ColorHex = "#10B981",
            SortOrder = 1,
            IsPersisted = false
        });
        await app.ShelfSession.AddItemAsync(new ShelfItem
        {
            ShelfId = photoShelfId,
            Type = ShelfItemType.File,
            FilePath = @"C:\Users\me\Pictures\san-pham-01.jpg",
            AddedAt = now,
            SortOrder = 0
        });
    }

    private static async Task CaptureAsync(Window window, string outputPath)
    {
        // Let the window's own async loading finish, then let WPF finish a full layout +
        // render pass before reading pixels back — otherwise the capture can catch a
        // half-arranged tree and produce a blank or clipped image.
        await Task.Delay(400);
        window.UpdateLayout();
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);

        if (window.Content is not FrameworkElement root)
        {
            throw new InvalidOperationException($"{window.GetType().Name} has no FrameworkElement content to render.");
        }

        var width = root.ActualWidth > 0 ? root.ActualWidth : window.Width;
        var height = root.ActualHeight > 0 ? root.ActualHeight : window.Height;
        if (double.IsNaN(width) || double.IsNaN(height) || width <= 0 || height <= 0)
        {
            throw new InvalidOperationException(
                $"{window.GetType().Name} rendered with a non-positive size ({width}x{height}).");
        }

        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(width * RenderScale),
            (int)Math.Ceiling(height * RenderScale),
            96 * RenderScale,
            96 * RenderScale,
            PixelFormats.Pbgra32);
        bitmap.Render(root);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(outputPath);
        encoder.Save(stream);

        Log($"Captured {Path.GetFileName(outputPath)} ({bitmap.PixelWidth}x{bitmap.PixelHeight})");
    }
}

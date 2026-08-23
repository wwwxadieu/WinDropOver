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
                // Only ever set in screenshot mode: swallowing it keeps the harness alive so the
                // remaining windows still get captured and the run ends with a real report
                // instead of the process vanishing. The per-window try/catch records the failure.
                e.Handled = true;
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
        await SeedSampleDataAsync(app, outputDirectory);
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
                // A step may hand back a window an earlier step already opened; closing one twice
                // at the end of the run throws.
                if (!opened.Contains(window))
                {
                    opened.Add(window);
                }
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
            await window.ShowAtPointAsync(600, 400);
            bubble = window;
            return window;
        });

        // The same card switched to a different shelf, not a second one: the shelf's glass is a
        // blurred photograph of whatever it is covering, so a second card opened over the first
        // would come out with the first one frosted into its own background.
        await StepAsync("bubble (detail grid)", "02b-bubble-thumbnails.png", async () =>
        {
            if (bubble is null)
            {
                throw new InvalidOperationException("Bubble window failed, so it cannot be switched to the photo shelf.");
            }
            var shelves = await app.ShelfSession.GetShelvesAsync();
            var photoShelf = shelves.First(sh => sh.Name == "Ảnh gửi khách");
            await bubble.ShowShelfAsync(photoShelf.Id, 600, 400);
            // Opened to the detail view on purpose: the card defaults to the fanned stack, so the
            // grid layout and the thumbnail decode behind it would otherwise never be drawn here.
            await bubble.ShowDetailsAsync();
            return bubble;
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
    private static async Task SeedSampleDataAsync(App app, string outputDirectory)
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

        // Written to disk for the same reason the photo shelf below is: each row asks the shell
        // for the file's icon and the filesystem for its size, and a path that does not exist
        // exercises neither — it just renders "không tìm thấy" and leaves both paths untested on
        // every CI run. The sizes differ so the second line is visibly saying something.
        var workDirectory = Path.Combine(outputDirectory, "screenshot-data", "documents");
        Directory.CreateDirectory(workDirectory);
        var workFiles = new[]
        {
            ("bao-cao-q3.docx", 184_320),
            ("so-lieu.xlsx", 27_648),
            ("ghi-chu.txt", 1_204)
        };
        for (var i = 0; i < workFiles.Length; i++)
        {
            var (name, size) = workFiles[i];
            var path = Path.Combine(workDirectory, name);
            File.WriteAllBytes(path, new byte[size]);

            await app.ShelfSession.AddItemAsync(new ShelfItem
            {
                ShelfId = workShelfId,
                Type = ShelfItemType.File,
                FilePath = path,
                AddedAt = now.AddMinutes(-i),
                SortOrder = i
            });
        }

        // A second shelf so the panel's tab strip is visibly doing its job. Its files are written
        // to disk for real, unlike the ones above: the card switches to its thumbnail grid only
        // when every item is an image, and decodes each one to draw it. Seeding paths that do not
        // exist would leave both the grid layout and the decode untested on every CI run — which
        // is the whole reason this harness exists.
        var photoShelfId = await app.ShelfSession.CreateShelfAsync(new Shelf
        {
            Name = "Ảnh gửi khách",
            ColorHex = "#10B981",
            SortOrder = 1,
            IsPersisted = false
        });

        var photoDirectory = Path.Combine(outputDirectory, "screenshot-data", "photos");
        Directory.CreateDirectory(photoDirectory);
        var swatches = new[] { Colors.SteelBlue, Colors.IndianRed, Colors.SeaGreen, Colors.Goldenrod };
        for (var i = 0; i < swatches.Length; i++)
        {
            var path = Path.Combine(photoDirectory, $"san-pham-{i + 1:00}.png");
            WriteSolidPng(path, swatches[i]);
            await app.ShelfSession.AddItemAsync(new ShelfItem
            {
                ShelfId = photoShelfId,
                Type = ShelfItemType.File,
                FilePath = path,
                AddedAt = now.AddMinutes(-i),
                SortOrder = i
            });
        }
    }

    /// <summary>A small real PNG, so the thumbnail path has something it can genuinely decode.</summary>
    private static void WriteSolidPng(string path, Color color)
    {
        var bitmap = new WriteableBitmap(64, 64, 96, 96, PixelFormats.Bgra32, null);
        var pixels = new uint[64 * 64];
        var packed = (uint)((color.A << 24) | (color.R << 16) | (color.G << 8) | color.B);
        Array.Fill(pixels, packed);
        bitmap.WritePixels(new Int32Rect(0, 0, 64, 64), pixels, 64 * 4, 0);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
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

        // Only the window's Content is rendered, so a background set on the Window itself would
        // be lost — SettingsWindow paints #202020 there and its light text came out white on
        // white. Draw that background first, then the content over it, so the capture shows what
        // the user actually sees. Windows whose Content carries its own chrome (the overlays,
        // which are transparent by design) are unaffected.
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            if (window.Background is not null)
            {
                context.DrawRectangle(window.Background, null, new Rect(0, 0, width, height));
            }
            context.DrawRectangle(new VisualBrush(root), null, new Rect(0, 0, width, height));
        }

        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(width * RenderScale),
            (int)Math.Ceiling(height * RenderScale),
            96 * RenderScale,
            96 * RenderScale,
            PixelFormats.Pbgra32);
        bitmap.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(outputPath);
        encoder.Save(stream);

        Log($"Captured {Path.GetFileName(outputPath)} ({bitmap.PixelWidth}x{bitmap.PixelHeight})");
    }
}

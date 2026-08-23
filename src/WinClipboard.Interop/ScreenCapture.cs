using System.Runtime.InteropServices;
using WinClipboard.Interop.Native;

namespace WinClipboard.Interop;

/// <summary>A rectangle of the desktop as raw 32-bit BGRX pixels, top row first.</summary>
public sealed record CapturedRegion(int Width, int Height, byte[] Pixels);

/// <summary>
/// Copies a rectangle of the composed desktop.
///
/// This is what makes the shelf look like glass. Windows will blur behind a window for you —
/// DWM's acrylic backdrop — but only for a window that is not per-pixel transparent, and the
/// shelf has to be per-pixel transparent to have rounded corners, a soft shadow and an opening
/// animation at all. Rather than give those up, the card takes a picture of whatever it is about
/// to cover and blurs that itself. The result is the same frosted pane; it just doesn't keep
/// tracking what moves behind it, which for a card that shows up for a few seconds at a time is
/// a difference nobody is in a position to notice.
///
/// Returns null on any failure — a card with a flat background is a small loss, a crash is not.
/// </summary>
public static class ScreenCapture
{
    /// <summary>Coordinates are physical screen pixels, the same ones GetWindowRect and the mouse hook speak.</summary>
    public static CapturedRegion? Capture(int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        var screenDc = IntPtr.Zero;
        var memoryDc = IntPtr.Zero;
        var bitmap = IntPtr.Zero;
        var previous = IntPtr.Zero;

        try
        {
            screenDc = NativeMethods.GetDC(IntPtr.Zero);
            if (screenDc == IntPtr.Zero)
            {
                return null;
            }

            memoryDc = NativeMethods.CreateCompatibleDC(screenDc);
            bitmap = NativeMethods.CreateCompatibleBitmap(screenDc, width, height);
            if (memoryDc == IntPtr.Zero || bitmap == IntPtr.Zero)
            {
                return null;
            }

            previous = NativeMethods.SelectObject(memoryDc, bitmap);
            if (!NativeMethods.BitBlt(memoryDc, 0, 0, width, height, screenDc, x, y,
                    NativeConstants.SRCCOPY | NativeConstants.CAPTUREBLT))
            {
                return null;
            }

            // GetDIBits refuses to read a bitmap that is still selected into a DC.
            NativeMethods.SelectObject(memoryDc, previous);
            previous = IntPtr.Zero;

            var header = new BITMAPINFO
            {
                biSize = (uint)Marshal.SizeOf<BITMAPINFO>(),
                biWidth = width,
                biHeight = -height,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = NativeConstants.BI_RGB
            };

            var pixels = new byte[width * height * 4];
            var copied = NativeMethods.GetDIBits(
                memoryDc, bitmap, 0, (uint)height, pixels, ref header, NativeConstants.DIB_RGB_COLORS);

            return copied == height ? new CapturedRegion(width, height, pixels) : null;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (previous != IntPtr.Zero)
            {
                NativeMethods.SelectObject(memoryDc, previous);
            }
            if (bitmap != IntPtr.Zero)
            {
                NativeMethods.DeleteObject(bitmap);
            }
            if (memoryDc != IntPtr.Zero)
            {
                NativeMethods.DeleteDC(memoryDc);
            }
            if (screenDc != IntPtr.Zero)
            {
                NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
            }
        }
    }
}

using System.Runtime.InteropServices;
using LegendLauncher.Core.GameColor;
using LegendLauncher.Infrastructure.Logging;

namespace LegendLauncher.GameHost.Legacy;

/// <summary>
/// Color saturation slider of the launcher, applied to what the Flash OCX shows (some players found the game too colorful).
/// Flash presents its frames with GDI (BitBlt / StretchBlt / StretchDIBits, imported directly by the OCX). Those three imports of
/// the OCX ONLY are redirected: when the destination is a window (the frame the player sees) and the slider is below 100, the frame
/// is copied to a private 32-bit bitmap, desaturated there and then shown. Flash's own buffers are never modified (no cumulative
/// graying), internal memory-to-memory copies and PrintWindow captures (the macro's eyes) keep the original colors, and at 100 the
/// calls go straight to GDI with no copy at all.
/// </summary>
internal static class FlashColorFilter
{
    private const uint SourceCopy = 0x00CC0020;
    private const uint DibRgbColors = 0;

    private static readonly object s_gate = new();
    private static readonly GameColorChannel.Reader s_channel = new();

    // Kept alive for the whole process: the patched import table points at them.
    private static BitBltProc? s_bitBlt;
    private static StretchBltProc? s_stretchBlt;
    private static StretchDIBitsProc? s_stretchDIBits;
    private static int s_installed;

    private static nint s_scratchDc;
    private static nint s_scratchBitmap;
    private static nint s_scratchBits;
    private static int s_scratchWidth;
    private static int s_scratchHeight;

    private static long s_calls;
    private static long s_filtered;
    private static int s_reported;

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private delegate bool BitBltProc(nint hdc, int x, int y, int cx, int cy, nint hdcSrc, int x1, int y1, uint rop);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private delegate bool StretchBltProc(
        nint hdcDest, int xDest, int yDest, int wDest, int hDest, nint hdcSrc, int xSrc, int ySrc, int wSrc, int hSrc, uint rop);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int StretchDIBitsProc(
        nint hdc, int xDest, int yDest, int destWidth, int destHeight, int xSrc, int ySrc, int srcWidth, int srcHeight,
        nint bits, nint bitmapInfo, uint usage, uint rop);

    /// <summary>How many of the three drawing imports were redirected (diagnostics/tests).</summary>
    internal static int PatchedImports { get; private set; }

    public static void Install(string flashOcxPath)
    {
        if (Interlocked.CompareExchange(ref s_installed, 1, 0) != 0)
        {
            return;
        }

        try
        {
            nint module = ImportTablePatcher.FindModule(flashOcxPath);
            if (module == nint.Zero)
            {
                Interlocked.Exchange(ref s_installed, 0);
                return;
            }

            s_bitBlt = BitBltHook;
            s_stretchBlt = StretchBltHook;
            s_stretchDIBits = StretchDIBitsHook;
            int patched = 0;
            patched += ImportTablePatcher.Patch(module, "gdi32.dll", "BitBlt", Marshal.GetFunctionPointerForDelegate(s_bitBlt));
            patched += ImportTablePatcher.Patch(module, "gdi32.dll", "StretchBlt", Marshal.GetFunctionPointerForDelegate(s_stretchBlt));
            patched += ImportTablePatcher.Patch(module, "gdi32.dll", "StretchDIBits", Marshal.GetFunctionPointerForDelegate(s_stretchDIBits));
            PatchedImports = patched;
        }
        catch (Exception exception) when (
            exception is AccessViolationException or ArgumentException or InvalidOperationException or ExternalException)
        {
            DiagnosticLog.Current.WriteFailure(
                "gamehost.color_filter",
                "The Flash color filter could not be installed; the game keeps its original colors.",
                exception,
                ("flashOcxPath", flashOcxPath));
        }
    }

    private static int Saturation()
    {
        try
        {
            return s_channel.Current;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ObjectDisposedException)
        {
            return GameColorChannel.Original;
        }
    }

    /// <summary>True when this call shows a frame on a window and the slider asks for less color.</summary>
    private static bool ShouldFilter(nint hdcDest, uint rop, out int saturation)
    {
        Interlocked.Increment(ref s_calls);
        saturation = GameColorChannel.Original;
        if (rop != SourceCopy || hdcDest == nint.Zero || WindowFromDC(hdcDest) == nint.Zero)
        {
            return false;
        }

        saturation = Saturation();
        return saturation < GameColorChannel.Original;
    }

    private static bool BitBltHook(nint hdc, int x, int y, int cx, int cy, nint hdcSrc, int x1, int y1, uint rop)
    {
        if (cx <= 0 || cy <= 0 || hdcSrc == nint.Zero || !ShouldFilter(hdc, rop, out int saturation))
        {
            return BitBlt(hdc, x, y, cx, cy, hdcSrc, x1, y1, rop);
        }

        lock (s_gate)
        {
            if (!EnsureScratch(cx, cy) || !BitBlt(s_scratchDc, 0, 0, cx, cy, hdcSrc, x1, y1, SourceCopy))
            {
                return BitBlt(hdc, x, y, cx, cy, hdcSrc, x1, y1, rop);
            }

            Desaturate(cx, cy, saturation);
            return BitBlt(hdc, x, y, cx, cy, s_scratchDc, 0, 0, SourceCopy);
        }
    }

    private static bool StretchBltHook(
        nint hdcDest, int xDest, int yDest, int wDest, int hDest, nint hdcSrc, int xSrc, int ySrc, int wSrc, int hSrc, uint rop)
    {
        if (wSrc <= 0 || hSrc <= 0 || hdcSrc == nint.Zero || !ShouldFilter(hdcDest, rop, out int saturation))
        {
            return StretchBlt(hdcDest, xDest, yDest, wDest, hDest, hdcSrc, xSrc, ySrc, wSrc, hSrc, rop);
        }

        lock (s_gate)
        {
            if (!EnsureScratch(wSrc, hSrc) || !BitBlt(s_scratchDc, 0, 0, wSrc, hSrc, hdcSrc, xSrc, ySrc, SourceCopy))
            {
                return StretchBlt(hdcDest, xDest, yDest, wDest, hDest, hdcSrc, xSrc, ySrc, wSrc, hSrc, rop);
            }

            Desaturate(wSrc, hSrc, saturation);
            return StretchBlt(hdcDest, xDest, yDest, wDest, hDest, s_scratchDc, 0, 0, wSrc, hSrc, SourceCopy);
        }
    }

    private static int StretchDIBitsHook(
        nint hdc, int xDest, int yDest, int destWidth, int destHeight, int xSrc, int ySrc, int srcWidth, int srcHeight,
        nint bits, nint bitmapInfo, uint usage, uint rop)
    {
        if (destWidth <= 0 || destHeight <= 0 || !ShouldFilter(hdc, rop, out int saturation))
        {
            return StretchDIBits(hdc, xDest, yDest, destWidth, destHeight, xSrc, ySrc, srcWidth, srcHeight, bits, bitmapInfo, usage, rop);
        }

        lock (s_gate)
        {
            int lines = EnsureScratch(destWidth, destHeight)
                ? StretchDIBits(s_scratchDc, 0, 0, destWidth, destHeight, xSrc, ySrc, srcWidth, srcHeight, bits, bitmapInfo, usage, SourceCopy)
                : 0;
            if (lines == 0)
            {
                return StretchDIBits(hdc, xDest, yDest, destWidth, destHeight, xSrc, ySrc, srcWidth, srcHeight, bits, bitmapInfo, usage, rop);
            }

            Desaturate(destWidth, destHeight, saturation);
            return BitBlt(hdc, xDest, yDest, destWidth, destHeight, s_scratchDc, 0, 0, SourceCopy) ? lines : 0;
        }
    }

    private static void Desaturate(int width, int height, int saturation)
    {
        GdiFlush();
        GameColorMath.Desaturate(s_scratchBits, s_scratchWidth * 4, width, height, saturation);
        Interlocked.Increment(ref s_filtered);
        ReportOnce();
    }

    /// <summary>A private top-down 32-bit bitmap at least as large as the frame (grows, never shrinks).</summary>
    private static bool EnsureScratch(int width, int height)
    {
        if (s_scratchDc != nint.Zero && width <= s_scratchWidth && height <= s_scratchHeight)
        {
            return true;
        }

        int newWidth = Math.Max(width, s_scratchWidth);
        int newHeight = Math.Max(height, s_scratchHeight);
        if (s_scratchDc == nint.Zero)
        {
            s_scratchDc = CreateCompatibleDC(nint.Zero);
            if (s_scratchDc == nint.Zero)
            {
                return false;
            }
        }

        var header = new BitmapInfoHeader
        {
            Size = Marshal.SizeOf<BitmapInfoHeader>(),
            Width = newWidth,
            Height = -newHeight,
            Planes = 1,
            BitCount = 32,
        };
        nint bitmap = CreateDIBSection(s_scratchDc, ref header, DibRgbColors, out nint bitsPointer, nint.Zero, 0);
        if (bitmap == nint.Zero)
        {
            return false;
        }

        _ = SelectObject(s_scratchDc, bitmap);
        if (s_scratchBitmap != nint.Zero)
        {
            DeleteObject(s_scratchBitmap);
        }

        s_scratchBitmap = bitmap;
        s_scratchBits = bitsPointer;
        s_scratchWidth = newWidth;
        s_scratchHeight = newHeight;
        return true;
    }

    /// <summary>One diagnostic line per session, so a test can tell whether the filter really sees Flash's frames.</summary>
    private static void ReportOnce()
    {
        if (Interlocked.CompareExchange(ref s_reported, 1, 0) != 0)
        {
            return;
        }

        DiagnosticLog.Current.WriteFailure(
            "gamehost.color_filter_active",
            "The color filter is desaturating Flash frames (informational).",
            exception: null,
            ("drawCalls", Interlocked.Read(ref s_calls).ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("patchedImports", PatchedImports.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public int Size;
        public int Width;
        public int Height;
        public short Planes;
        public short BitCount;
        public int Compression;
        public int SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public int ClrUsed;
        public int ClrImportant;
    }

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BitBlt(nint hdc, int x, int y, int cx, int cy, nint hdcSrc, int x1, int y1, uint rop);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool StretchBlt(
        nint hdcDest, int xDest, int yDest, int wDest, int hDest, nint hdcSrc, int xSrc, int ySrc, int wSrc, int hSrc, uint rop);

    [DllImport("gdi32.dll")]
    private static extern int StretchDIBits(
        nint hdc, int xDest, int yDest, int destWidth, int destHeight, int xSrc, int ySrc, int srcWidth, int srcHeight,
        nint bits, nint bitmapInfo, uint usage, uint rop);

    [DllImport("gdi32.dll")]
    private static extern nint CreateCompatibleDC(nint hdc);

    [DllImport("gdi32.dll")]
    private static extern nint CreateDIBSection(
        nint hdc, ref BitmapInfoHeader header, uint usage, out nint bits, nint section, uint offset);

    [DllImport("gdi32.dll")]
    private static extern nint SelectObject(nint hdc, nint gdiObject);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(nint gdiObject);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GdiFlush();

    [DllImport("user32.dll")]
    private static extern nint WindowFromDC(nint hdc);
}

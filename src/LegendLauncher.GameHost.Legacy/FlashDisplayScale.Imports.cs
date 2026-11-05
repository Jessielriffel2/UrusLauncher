using System.Runtime.InteropServices;

namespace LegendLauncher.GameHost.Legacy;

/// <summary>
/// The user32 calls of the Flash OCX that describe its window. Each hook only acts on the Flash window of this process while the
/// zoom is on; everything else goes straight to Windows.
/// </summary>
internal static partial class FlashDisplayScale
{
    private const int WindowMessageSize = 0x0005;
    private const uint SetWindowPosNoSize = 0x0001;
    private const uint SetWindowPosNoMove = 0x0002;

    /// <summary>Imports that must all be redirected for the zoom to be consistent (11 of user32, 2 of gdi32).</summary>
    private const int RequiredImports = 13;

    // Kept alive for the whole process: the patched import table points at them.
    private static GetClientRectProc? s_getClientRect;
    private static PointProc? s_screenToClient;
    private static PointProc? s_clientToScreen;
    private static MapWindowPointsProc? s_mapWindowPoints;
    private static SetWindowPosProc? s_setWindowPos;
    private static MoveWindowProc? s_moveWindow;
    private static InvalidateRectProc? s_invalidateRect;
    private static RedrawWindowProc? s_redrawWindow;
    private static BeginPaintProc? s_beginPaint;
    private static SetCaretPosProc? s_setCaretPos;
    private static CreateCaretProc? s_createCaret;
    private static GetClipBoxProc? s_getClipBox;
    private static RectVisibleProc? s_rectVisible;

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private delegate bool GetClientRectProc(nint window, nint rect);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private delegate bool PointProc(nint window, nint point);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int MapWindowPointsProc(nint from, nint to, nint points, uint count);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private delegate bool SetWindowPosProc(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private delegate bool MoveWindowProc(nint window, int x, int y, int width, int height, [MarshalAs(UnmanagedType.Bool)] bool repaint);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private delegate bool InvalidateRectProc(nint window, nint rect, [MarshalAs(UnmanagedType.Bool)] bool erase);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private delegate bool RedrawWindowProc(nint window, nint rect, nint region, uint flags);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint BeginPaintProc(nint window, nint paint);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private delegate bool SetCaretPosProc(int x, int y);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private delegate bool CreateCaretProc(nint window, nint bitmap, int width, int height);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int GetClipBoxProc(nint deviceContext, nint rect);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private delegate bool RectVisibleProc(nint deviceContext, nint rect);

    private static int PatchImports(nint module)
    {
        s_getClientRect = GetClientRectHook;
        s_screenToClient = ScreenToClientHook;
        s_clientToScreen = ClientToScreenHook;
        s_mapWindowPoints = MapWindowPointsHook;
        s_setWindowPos = SetWindowPosHook;
        s_moveWindow = MoveWindowHook;
        s_invalidateRect = InvalidateRectHook;
        s_redrawWindow = RedrawWindowHook;
        s_beginPaint = BeginPaintHook;
        s_setCaretPos = SetCaretPosHook;
        s_createCaret = CreateCaretHook;
        s_getClipBox = GetClipBoxHook;
        s_rectVisible = RectVisibleHook;
        int patched = 0;
        patched += Patch(module, "GetClientRect", s_getClientRect);
        patched += Patch(module, "ScreenToClient", s_screenToClient);
        patched += Patch(module, "ClientToScreen", s_clientToScreen);
        patched += Patch(module, "MapWindowPoints", s_mapWindowPoints);
        patched += Patch(module, "SetWindowPos", s_setWindowPos);
        patched += Patch(module, "MoveWindow", s_moveWindow);
        patched += Patch(module, "InvalidateRect", s_invalidateRect);
        patched += Patch(module, "RedrawWindow", s_redrawWindow);
        patched += Patch(module, "BeginPaint", s_beginPaint);
        patched += Patch(module, "SetCaretPos", s_setCaretPos);
        patched += Patch(module, "CreateCaret", s_createCaret);
        // On the window DC Flash asks what is visible (once per paint and twice per frame, measured): game pixels too.
        patched += Patch(module, "GetClipBox", s_getClipBox, "gdi32.dll");
        patched += Patch(module, "RectVisible", s_rectVisible, "gdi32.dll");
        return patched;
    }

    private static int Patch(nint module, string function, Delegate hook, string library = "user32.dll") =>
        ImportTablePatcher.Patch(module, library, function, Marshal.GetFunctionPointerForDelegate(hook));

    private static bool IsFlashDeviceContext(nint deviceContext) =>
        deviceContext != nint.Zero && (deviceContext == s_printDc || WindowFromDC(deviceContext) == s_window);

    /// <summary>
    /// The visible area of the window DC is physical; Flash intersects it with the area it repaints (game pixels), so it gets
    /// the box in game pixels (rounded outward). Without this a repaint after an expose only covered part of the area.
    /// </summary>
    private static int GetClipBoxHook(nint deviceContext, nint rect)
    {
        int result = GetClipBox(deviceContext, rect);
        double scale = Scale;
        if (scale <= 1 || rect == nint.Zero || !IsFlashDeviceContext(deviceContext))
        {
            return result;
        }

        NativeRect physical = Marshal.PtrToStructure<NativeRect>(rect);
        Marshal.StructureToPtr(ToLogicalOutward(physical, scale), rect, false);
        return result;
    }

    private static bool RectVisibleHook(nint deviceContext, nint rect)
    {
        double scale = Scale;
        if (scale <= 1 || rect == nint.Zero || !IsFlashDeviceContext(deviceContext))
        {
            return RectVisible(deviceContext, rect);
        }

        NativeRect physical = ToPhysicalOutward(Marshal.PtrToStructure<NativeRect>(rect), scale);
        nint buffer = Marshal.AllocHGlobal(Marshal.SizeOf<NativeRect>());
        try
        {
            Marshal.StructureToPtr(physical, buffer, false);
            return RectVisible(deviceContext, buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static NativeRect ToLogicalOutward(NativeRect physical, double scale) =>
        new(
            (int)Math.Floor(physical.Left / scale),
            (int)Math.Floor(physical.Top / scale),
            (int)Math.Ceiling(physical.Right / scale),
            (int)Math.Ceiling(physical.Bottom / scale));

    /// <summary>The render buffer of Flash has the client size: game pixels, not physical ones.</summary>
    private static bool GetClientRectHook(nint window, nint rect)
    {
        bool result = GetClientRect(window, rect);
        double scale = Scale;
        if (!result || window != s_window || scale <= 1 || rect == nint.Zero)
        {
            return result;
        }

        NativeRect physical = Marshal.PtrToStructure<NativeRect>(rect);
        int width = physical.Width == s_physical.Width ? s_logical.Width : Math.Max(1, (int)Math.Round(physical.Width / scale));
        int height = physical.Height == s_physical.Height ? s_logical.Height : Math.Max(1, (int)Math.Round(physical.Height / scale));
        Marshal.StructureToPtr(new NativeRect(0, 0, width, height), rect, false);
        return true;
    }

    /// <summary>Mouse position asked by Flash (GetCursorPos + ScreenToClient): game pixels.</summary>
    private static bool ScreenToClientHook(nint window, nint point)
    {
        bool result = ScreenToClient(window, point);
        double scale = Scale;
        if (result && window == s_window && scale > 1 && point != nint.Zero)
        {
            NativePoint value = Marshal.PtrToStructure<NativePoint>(point);
            value.X = ToLogicalCoordinate(value.X, scale);
            value.Y = ToLogicalCoordinate(value.Y, scale);
            Marshal.StructureToPtr(value, point, false);
        }

        return result;
    }

    /// <summary>Game pixels to the screen (context menu, text input position): enlarged first.</summary>
    private static bool ClientToScreenHook(nint window, nint point)
    {
        double scale = Scale;
        if (window == s_window && scale > 1 && point != nint.Zero)
        {
            NativePoint value = Marshal.PtrToStructure<NativePoint>(point);
            value.X = ToPhysical(value.X, scale);
            value.Y = ToPhysical(value.Y, scale);
            Marshal.StructureToPtr(value, point, false);
        }

        return ClientToScreen(window, point);
    }

    private static int MapWindowPointsHook(nint from, nint to, nint points, uint count)
    {
        double scale = Scale;
        bool active = scale > 1 && points != nint.Zero && count > 0 && count <= 64;
        if (active && from == s_window)
        {
            ScalePoints(points, count, value => ToPhysical(value, scale));
        }

        int result = MapWindowPoints(from, to, points, count);
        if (active && to == s_window)
        {
            ScalePoints(points, count, value => ToLogicalCoordinate(value, scale));
        }

        return result;
    }

    private static void ScalePoints(nint points, uint count, Func<int, int> convert)
    {
        int size = Marshal.SizeOf<NativePoint>();
        for (int index = 0; index < count; index++)
        {
            nint address = points + (index * size);
            NativePoint value = Marshal.PtrToStructure<NativePoint>(address);
            value.X = convert(value.X);
            value.Y = convert(value.Y);
            Marshal.StructureToPtr(value, address, false);
        }
    }

    /// <summary>
    /// Flash places its own window at the (game-pixel) rectangle it received; the window itself stays physical. The rectangle
    /// remembered in <see cref="SetObjectRectsHook"/> is restored exactly, so rounding never makes WinForms see a different
    /// size and start a resize loop.
    /// </summary>
    private static bool SetWindowPosHook(nint window, nint insertAfter, int x, int y, int width, int height, uint flags)
    {
        if (window == s_window && Scale > 1)
        {
            (x, y, width, height) = ToPhysicalPlacement(x, y, width, height, flags);
        }

        return SetWindowPos(window, insertAfter, x, y, width, height, flags);
    }

    private static bool MoveWindowHook(nint window, int x, int y, int width, int height, bool repaint)
    {
        if (window == s_window && Scale > 1)
        {
            (x, y, width, height) = ToPhysicalPlacement(x, y, width, height, flags: 0);
        }

        return MoveWindow(window, x, y, width, height, repaint);
    }

    private static (int X, int Y, int Width, int Height) ToPhysicalPlacement(int x, int y, int width, int height, uint flags)
    {
        double scale = Scale;
        if ((flags & SetWindowPosNoMove) == 0)
        {
            bool remembered = x == s_logical.Left && y == s_logical.Top;
            x = remembered ? s_physical.Left : ToPhysical(x, scale);
            y = remembered ? s_physical.Top : ToPhysical(y, scale);
        }

        if ((flags & SetWindowPosNoSize) == 0)
        {
            bool remembered = width == s_logical.Width && height == s_logical.Height;
            width = remembered ? s_physical.Width : ToPhysical(width, scale);
            height = remembered ? s_physical.Height : ToPhysical(height, scale);
        }

        return (x, y, width, height);
    }

    /// <summary>Areas Flash marks as changed are game pixels: the physical area to repaint is larger (rounded outward).</summary>
    private static bool InvalidateRectHook(nint window, nint rect, bool erase)
    {
        if (window != s_window || rect == nint.Zero || Scale <= 1)
        {
            return InvalidateRect(window, rect, erase);
        }

        NativeRect physical = ToPhysicalOutward(Marshal.PtrToStructure<NativeRect>(rect), Scale);
        nint buffer = Marshal.AllocHGlobal(Marshal.SizeOf<NativeRect>());
        try
        {
            Marshal.StructureToPtr(physical, buffer, false);
            return InvalidateRect(window, buffer, erase);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static bool RedrawWindowHook(nint window, nint rect, nint region, uint flags)
    {
        if (window != s_window || Scale <= 1 || (rect == nint.Zero && region == nint.Zero))
        {
            return RedrawWindow(window, rect, region, flags);
        }

        if (region != nint.Zero || rect == nint.Zero)
        {
            // A region in game pixels cannot be scaled cheaply: redraw the whole window instead (a superset).
            return RedrawWindow(window, nint.Zero, nint.Zero, flags);
        }

        NativeRect physical = ToPhysicalOutward(Marshal.PtrToStructure<NativeRect>(rect), Scale);
        nint buffer = Marshal.AllocHGlobal(Marshal.SizeOf<NativeRect>());
        try
        {
            Marshal.StructureToPtr(physical, buffer, false);
            return RedrawWindow(window, buffer, nint.Zero, flags);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>The area Windows asks to repaint is physical: Flash receives it in game pixels (rounded outward).</summary>
    private static nint BeginPaintHook(nint window, nint paint)
    {
        nint deviceContext = BeginPaint(window, paint);
        double scale = Scale;
        if (deviceContext == nint.Zero || window != s_window || scale <= 1 || paint == nint.Zero)
        {
            return deviceContext;
        }

        // PAINTSTRUCT: HDC hdc; BOOL fErase; RECT rcPaint; ...
        nint paintRect = paint + nint.Size + sizeof(int);
        NativeRect logical = ToLogicalOutward(Marshal.PtrToStructure<NativeRect>(paintRect), scale);
        Marshal.StructureToPtr(
            new NativeRect(
                Math.Max(0, logical.Left),
                Math.Max(0, logical.Top),
                Math.Min(s_logical.Width, logical.Right),
                Math.Min(s_logical.Height, logical.Bottom)),
            paintRect,
            false);
        return deviceContext;
    }

    /// <summary>The text cursor (and the IME window that follows it) sits where Flash draws it, in physical pixels.</summary>
    private static bool SetCaretPosHook(int x, int y)
    {
        double scale = Scale;
        return scale > 1 ? SetCaretPos(ToPhysical(x, scale), ToPhysical(y, scale)) : SetCaretPos(x, y);
    }

    private static bool CreateCaretHook(nint window, nint bitmap, int width, int height)
    {
        double scale = Scale;
        if (window == s_window && scale > 1 && (bitmap == nint.Zero || bitmap == 1))
        {
            width = width <= 0 ? width : Math.Max(1, ToPhysical(width, scale));
            height = height <= 0 ? height : Math.Max(1, ToPhysical(height, scale));
        }

        return CreateCaret(window, bitmap, width, height);
    }

    private static NativeRect ToPhysicalOutward(NativeRect logical, double scale) =>
        new(
            (int)Math.Floor(logical.Left * scale) - 1,
            (int)Math.Floor(logical.Top * scale) - 1,
            (int)Math.Ceiling(logical.Right * scale) + 1,
            (int)Math.Ceiling(logical.Bottom * scale) + 1);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(nint window, nint rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ScreenToClient(nint window, nint point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(nint window, nint point);

    [DllImport("user32.dll")]
    private static extern int MapWindowPoints(nint from, nint to, nint points, uint count);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MoveWindow(nint window, int x, int y, int width, int height, [MarshalAs(UnmanagedType.Bool)] bool repaint);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InvalidateRect(nint window, nint rect, [MarshalAs(UnmanagedType.Bool)] bool erase);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RedrawWindow(nint window, nint rect, nint region, uint flags);

    [DllImport("user32.dll")]
    private static extern nint BeginPaint(nint window, nint paint);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCaretPos(int x, int y);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateCaret(nint window, nint bitmap, int width, int height);

    [DllImport("user32.dll")]
    private static extern nint WindowFromDC(nint deviceContext);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint window);

    [DllImport("gdi32.dll")]
    private static extern int GetClipBox(nint deviceContext, nint rect);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RectVisible(nint deviceContext, nint rect);

    [DllImport("gdi32.dll")]
    private static extern int SetStretchBltMode(nint deviceContext, int mode);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetBrushOrgEx(nint deviceContext, int x, int y, nint previous);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool StretchBlt(
        nint destination, int x, int y, int width, int height, nint source, int sourceX, int sourceY, int sourceWidth,
        int sourceHeight, uint rasterOperation);
}

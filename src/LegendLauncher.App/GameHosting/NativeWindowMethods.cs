using System.ComponentModel;
using System.Runtime.InteropServices;

namespace LegendLauncher.App.GameHosting;

/// <summary>
/// Narrow Win32 surface used to host an isolated GameHost window under a launcher-owned proxy HWND.
/// </summary>
internal static class NativeWindowMethods
{
    internal const uint WindowStylePopup = 0x80000000;
    internal const uint WindowStyleChild = 0x40000000;
    internal const uint WindowStyleMinimizeBox = 0x00020000;
    internal const uint WindowStyleMaximizeBox = 0x00010000;
    internal const uint WindowStyleThickFrame = 0x00040000;
    internal const uint WindowStyleSystemMenu = 0x00080000;
    internal const uint WindowStyleCaption = 0x00C00000;
    internal const uint WindowStyleClipChildren = 0x02000000;
    internal const uint WindowStyleClipSiblings = 0x04000000;
    internal const uint WindowStyleVisible = 0x10000000;

    private const int WindowLongStyle = -16;
    private const int WindowLongExStyle = -20;
    private const uint StaticBlackRectangle = 0x00000004;
    private const uint WindowExStyleLayered = 0x00080000;
    private const uint WindowExStyleNoActivate = 0x08000000;
    private const uint WindowExStyleTransparent = 0x00000020;
    private const uint WindowExStyleToolWindow = 0x00000080;
    private const uint WindowExtendedStyleNoParentNotify = 0x00000004;
    private const uint WindowExtendedStyleToolWindow = 0x00000080;
    private const uint WindowExtendedStyleNoActivate = 0x08000000;
    private const int ShowWindowHide = 0;
    private const int ShowWindowShow = 5;
    private const uint SetWindowPositionNoSize = 0x0001;
    private const uint SetWindowPositionNoMove = 0x0002;
    private const uint SetWindowPositionNoZOrder = 0x0004;
    private const uint SetWindowPositionNoActivate = 0x0010;
    private const uint SetWindowPositionFrameChanged = 0x0020;

    private const uint EmbeddedChromeMask =
        WindowStylePopup |
        WindowStyleCaption |
        WindowStyleThickFrame |
        WindowStyleSystemMenu |
        WindowStyleMinimizeBox |
        WindowStyleMaximizeBox;

    private const uint EmbeddedRequiredStyles =
        WindowStyleChild |
        WindowStyleClipChildren |
        WindowStyleClipSiblings;

    internal static uint CalculateEmbeddedStyle(uint originalStyle) =>
        (originalStyle | EmbeddedRequiredStyles) & ~EmbeddedChromeMask;

    internal static uint CalculateParkedStyle(uint originalStyle) =>
        CalculateEmbeddedStyle(originalStyle) & ~WindowStyleVisible;

    internal static nint CreateHiddenParkingWindow()
    {
        nint parkingWindow = CreateWindowEx(
            WindowExtendedStyleNoParentNotify |
            WindowExtendedStyleToolWindow |
            WindowExtendedStyleNoActivate,
            "Static",
            "UrusGameHostParking",
            WindowStylePopup |
            WindowStyleClipChildren |
            WindowStyleClipSiblings |
            StaticBlackRectangle,
            -32000,
            -32000,
            1,
            1,
            nint.Zero,
            nint.Zero,
            nint.Zero,
            nint.Zero);
        if (parkingWindow == nint.Zero)
        {
            throw CreateLastWin32Exception("The GameHost parking window could not be created.");
        }

        HideWindow(parkingWindow);
        return parkingWindow;
    }

    internal static nint CreateProxyWindow(nint parentWindow)
    {
        if (parentWindow == nint.Zero || !IsWindowHandle(parentWindow))
        {
            throw new ArgumentException("A valid launcher parent window is required.", nameof(parentWindow));
        }

        nint proxyWindow = CreateWindowEx(
            WindowExtendedStyleNoParentNotify,
            "Static",
            null,
            WindowStyleChild |
            WindowStyleClipChildren |
            WindowStyleClipSiblings |
            StaticBlackRectangle,
            0,
            0,
            1,
            1,
            parentWindow,
            nint.Zero,
            nint.Zero,
            nint.Zero);
        if (proxyWindow == nint.Zero)
        {
            throw CreateLastWin32Exception("The launcher proxy window could not be created.");
        }

        ShowWindowNative(proxyWindow, ShowWindowShow);
        return proxyWindow;
    }

    internal static bool IsWindowHandle(nint windowHandle) =>
        windowHandle != nint.Zero && IsWindow(windowHandle);

    internal static bool IsWindowVisible(nint windowHandle) =>
        windowHandle != nint.Zero && IsWindowVisibleNative(windowHandle);

    internal static bool IsWindowMinimized(nint windowHandle) =>
        windowHandle != nint.Zero && IsIconic(windowHandle);

    internal static uint GetWindowProcessId(nint windowHandle)
    {
        uint threadId = GetWindowThreadProcessId(windowHandle, out uint processId);
        if (threadId == 0 || processId == 0)
        {
            throw CreateLastWin32Exception("The window process could not be identified.");
        }

        return processId;
    }

    internal static nint GetParentWindow(nint windowHandle) => GetParent(windowHandle);

    internal static uint GetWindowStyle(nint windowHandle)
    {
        Marshal.SetLastPInvokeError(0);
        nint style = IntPtr.Size == 8
            ? GetWindowLongPtr64(windowHandle, WindowLongStyle)
            : new nint(GetWindowLong32(windowHandle, WindowLongStyle));
        int error = Marshal.GetLastPInvokeError();
        if (style == nint.Zero && error != 0)
        {
            throw new Win32Exception(error, "The window style could not be read.");
        }

        return unchecked((uint)style.ToInt64());
    }

    internal static void SetWindowStyle(nint windowHandle, uint style)
    {
        Marshal.SetLastPInvokeError(0);
        nint previousStyle = IntPtr.Size == 8
            ? SetWindowLongPtr64(windowHandle, WindowLongStyle, unchecked((nint)(long)style))
            : new nint(SetWindowLong32(windowHandle, WindowLongStyle, unchecked((int)style)));
        int error = Marshal.GetLastPInvokeError();
        if (previousStyle == nint.Zero && error != 0)
        {
            throw new Win32Exception(error, "The window style could not be changed.");
        }

        if (!SetWindowPos(
                windowHandle,
                nint.Zero,
                0,
                0,
                0,
                0,
                SetWindowPositionNoSize |
                SetWindowPositionNoMove |
                SetWindowPositionNoZOrder |
                SetWindowPositionNoActivate |
                SetWindowPositionFrameChanged))
        {
            throw CreateLastWin32Exception("The window frame could not be refreshed.");
        }
    }

    internal static nint SetParentWindow(nint childWindow, nint parentWindow)
    {
        Marshal.SetLastPInvokeError(0);
        nint previousParent = SetParent(childWindow, parentWindow);
        int error = Marshal.GetLastPInvokeError();
        if (previousParent == nint.Zero && error != 0)
        {
            throw new Win32Exception(error, "The game window could not be attached to the launcher.");
        }

        return previousParent;
    }

    internal static NativeClientSize GetClientSize(nint windowHandle)
    {
        if (!GetClientRect(windowHandle, out NativeRectangle rectangle))
        {
            throw CreateLastWin32Exception("The launcher surface size could not be read.");
        }

        return new NativeClientSize(
            Math.Max(0, rectangle.Right - rectangle.Left),
            Math.Max(0, rectangle.Bottom - rectangle.Top));
    }

    internal static NativeClientGeometry GetClientGeometry(nint windowHandle)
    {
        NativeClientSize size = GetClientSize(windowHandle);
        var origin = new NativePoint(0, 0);
        if (!ClientToScreen(windowHandle, ref origin))
        {
            throw CreateLastWin32Exception("The launcher surface origin could not be read.");
        }

        return new NativeClientGeometry(origin.X, origin.Y, size.Width, size.Height);
    }

    internal static nint FindDeepestChildAtPoint(nint parentWindow, int x, int y)
    {
        if (!IsWindowHandle(parentWindow))
        {
            return nint.Zero;
        }

        nint current = parentWindow;
        var point = new NativePoint(x, y);
        var visited = new HashSet<nint>();
        while (visited.Add(current))
        {
            nint child = RealChildWindowFromPoint(current, point);
            if (child == nint.Zero || child == current)
            {
                return current;
            }

            var childPoint = point;
            if (!ScreenToClient(child, ref childPoint))
            {
                return child;
            }

            current = child;
            point = childPoint;
        }

        return current;
    }

    internal static bool TryClientToScreen(
        nint windowHandle,
        int x,
        int y,
        out int screenX,
        out int screenY)
    {
        var point = new NativePoint(x, y);
        if (!ClientToScreen(windowHandle, ref point))
        {
            screenX = x;
            screenY = y;
            return false;
        }

        screenX = point.X;
        screenY = point.Y;
        return true;
    }

    internal static bool TryScreenToClient(
        nint windowHandle,
        int screenX,
        int screenY,
        out int clientX,
        out int clientY)
    {
        var point = new NativePoint(screenX, screenY);
        if (!ScreenToClient(windowHandle, ref point))
        {
            clientX = screenX;
            clientY = screenY;
            return false;
        }

        clientX = point.X;
        clientY = point.Y;
        return true;
    }

    internal static bool PostMessage(
        nint windowHandle,
        uint message,
        nint wParam,
        nint lParam) =>
        PostMessageNative(windowHandle, message, wParam, lParam);

    internal static void SetOverlayBounds(nint windowHandle, int x, int y, int width, int height)
    {
        if (!SetWindowPos(
                windowHandle,
                nint.Zero,
                x,
                y,
                width,
                height,
                SetWindowPositionNoZOrder |
                SetWindowPositionNoActivate |
                SetWindowPositionFrameChanged))
        {
            throw CreateLastWin32Exception("The macro overlay could not be positioned.");
        }
    }

    internal static void SetClickThrough(nint windowHandle)
    {
        Marshal.SetLastPInvokeError(0);
        nint current = IntPtr.Size == 8
            ? GetWindowLongPtr64(windowHandle, WindowLongExStyle)
            : new nint(GetWindowLong32(windowHandle, WindowLongExStyle));
        int error = Marshal.GetLastPInvokeError();
        if (current == nint.Zero && error != 0)
        {
            throw new Win32Exception(error, "The overlay extended style could not be read.");
        }

        nint updated = current |
            new nint((long)(WindowExStyleLayered | WindowExStyleNoActivate | WindowExStyleTransparent | WindowExStyleToolWindow));
        nint previous = IntPtr.Size == 8
            ? SetWindowLongPtr64(windowHandle, WindowLongExStyle, updated)
            : new nint(SetWindowLong32(windowHandle, WindowLongExStyle, updated.ToInt32()));
        error = Marshal.GetLastPInvokeError();
        if (previous == nint.Zero && error != 0)
        {
            throw new Win32Exception(error, "The overlay extended style could not be changed.");
        }
    }

    internal static void ResizeWindow(nint windowHandle, NativeClientSize size)
    {
        if (!MoveWindow(windowHandle, 0, 0, size.Width, size.Height, repaint: true))
        {
            throw CreateLastWin32Exception("The game window could not be resized.");
        }
    }

    internal static void HideWindow(nint windowHandle) => ShowWindowNative(windowHandle, ShowWindowHide);

    internal static void ShowWindow(nint windowHandle) => ShowWindowNative(windowHandle, ShowWindowShow);

    internal static void ShowWindowAndFocus(nint windowHandle)
    {
        ShowWindowNative(windowHandle, ShowWindowShow);
        _ = SetFocus(windowHandle);
    }

    internal static void DestroyProxyWindow(nint proxyWindow)
    {
        if (proxyWindow == nint.Zero || !IsWindowHandle(proxyWindow))
        {
            return;
        }

        if (!DestroyWindow(proxyWindow))
        {
            throw CreateLastWin32Exception("The launcher proxy window could not be destroyed.");
        }
    }

    private static Win32Exception CreateLastWin32Exception(string message)
    {
        int error = Marshal.GetLastPInvokeError();
        return error == 0
            ? new Win32Exception(message)
            : new Win32Exception(error, message);
    }

    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowEx(
        uint extendedStyle,
        string className,
        string? windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        nint parentWindow,
        nint menu,
        nint instance,
        nint parameter);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisibleNative(nint windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(nint windowHandle, out uint processId);

    [DllImport("user32.dll")]
    private static extern nint GetParent(nint windowHandle);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr64(nint windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(nint windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr64(nint windowHandle, int index, nint newValue);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(nint windowHandle, int index, int newValue);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetParent(nint childWindow, nint newParentWindow);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(nint windowHandle, out NativeRectangle rectangle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(nint windowHandle, ref NativePoint point);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ScreenToClient(nint windowHandle, ref NativePoint point);

    [DllImport("user32.dll", EntryPoint = "RealChildWindowFromPoint", SetLastError = true)]
    private static extern nint RealChildWindowFromPoint(nint parentWindow, NativePoint point);

    [DllImport("user32.dll", EntryPoint = "PostMessage", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessageNative(
        nint windowHandle,
        uint message,
        nint wParam,
        nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MoveWindow(
        nint windowHandle,
        int x,
        int y,
        int width,
        int height,
        [MarshalAs(UnmanagedType.Bool)] bool repaint);

    [DllImport("user32.dll", EntryPoint = "ShowWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindowNative(nint windowHandle, int command);

    [DllImport("user32.dll")]
    private static extern nint SetFocus(nint windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        nint windowHandle,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public NativePoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X;
        public int Y;
    }
}

internal readonly record struct NativeClientGeometry(int X, int Y, int Width, int Height)
{
    public bool HasArea => Width > 0 && Height > 0;
}

internal readonly record struct NativeClientSize(int Width, int Height)
{
    public bool HasArea => Width > 0 && Height > 0;
}

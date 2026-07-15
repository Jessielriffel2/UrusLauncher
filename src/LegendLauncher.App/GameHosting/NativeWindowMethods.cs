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

    private const int WindowLongStyle = -16;
    private const uint StaticBlackRectangle = 0x00000004;
    private const uint WindowExtendedStyleNoParentNotify = 0x00000004;
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
}

internal readonly record struct NativeClientSize(int Width, int Height)
{
    public bool HasArea => Width > 0 && Height > 0;
}

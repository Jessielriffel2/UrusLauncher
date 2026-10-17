using System.Runtime.InteropServices;
using LegendLauncher.Infrastructure.Logging;

namespace LegendLauncher.GameHost.Legacy;

/// <summary>
/// The session window is embedded in the launcher window, so Windows joins the input state of both
/// processes: when Flash calls SetCursor/SetCapture/ReleaseCapture it changes the cursor and the mouse
/// capture of the WHOLE launcher. With two sessions side by side both Flash instances keep fighting over
/// the cursor (it flickers), and a macro click (posted WM_LBUTTONDOWN) makes Flash capture the mouse,
/// swallowing the player's clicks on the launcher buttons.
/// <para>
/// This patches the user32 imports of the Flash OCX only (never the system): those calls go through
/// when the real pointer is over a window of this session (or this session already holds the capture),
/// and become no-ops otherwise. The player's real mouse and the macro's virtual clicks stay independent.
/// </para>
/// </summary>
internal static class FlashPointerIsolation
{
    private const int VirtualKeyLeftButton = 0x01;
    private const int VirtualKeyRightButton = 0x02;

    // Kept alive for the whole process: the patched import table points at them.
    private static SetCursorProc? s_setCursor;
    private static SetCaptureProc? s_setCapture;
    private static ReleaseCaptureProc? s_releaseCapture;
    private static SetCursorPosProc? s_setCursorPos;
    private static int s_installed;

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint SetCursorProc(nint cursor);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint SetCaptureProc(nint window);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private delegate bool ReleaseCaptureProc();

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private delegate bool SetCursorPosProc(int x, int y);

    /// <summary>How many of the four pointer imports of the OCX were redirected (diagnostics).</summary>
    internal static int PatchedImports { get; private set; }

    /// <summary>Patches the loaded Flash OCX. Safe to call more than once; failures only log.</summary>
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
                DiagnosticLog.Current.WriteFailure(
                    "gamehost.pointer_isolation",
                    "The Flash OCX module was not loaded; pointer isolation was not installed.",
                    exception: null,
                    ("flashOcxPath", flashOcxPath));
                return;
            }

            s_setCursor = SetCursorHook;
            s_setCapture = SetCaptureHook;
            s_releaseCapture = ReleaseCaptureHook;
            s_setCursorPos = SetCursorPosHook;
            int patched = 0;
            patched += ImportTablePatcher.Patch(module, "user32.dll", "SetCursor", Marshal.GetFunctionPointerForDelegate(s_setCursor));
            patched += ImportTablePatcher.Patch(module, "user32.dll", "SetCapture", Marshal.GetFunctionPointerForDelegate(s_setCapture));
            patched += ImportTablePatcher.Patch(module, "user32.dll", "ReleaseCapture", Marshal.GetFunctionPointerForDelegate(s_releaseCapture));
            patched += ImportTablePatcher.Patch(module, "user32.dll", "SetCursorPos", Marshal.GetFunctionPointerForDelegate(s_setCursorPos));
            PatchedImports = patched;
            if (patched == 0)
            {
                DiagnosticLog.Current.WriteFailure(
                    "gamehost.pointer_isolation",
                    "No pointer import of the Flash OCX could be patched.",
                    exception: null,
                    ("flashOcxPath", flashOcxPath));
            }
        }
        catch (Exception exception) when (
            exception is AccessViolationException or ArgumentException or InvalidOperationException or
            ExternalException)
        {
            DiagnosticLog.Current.WriteFailure(
                "gamehost.pointer_isolation",
                "The Flash pointer isolation could not be installed.",
                exception,
                ("flashOcxPath", flashOcxPath));
        }
    }

    /// <summary>True when the player's real pointer is over (or captured by) a window of this session.</summary>
    internal static bool PointerBelongsToThisSession()
    {
        uint processId = (uint)Environment.ProcessId;
        nint capture = GetCapture();
        if (capture != nint.Zero && OwnerProcess(capture) == processId)
        {
            return true;
        }

        if (!GetCursorPos(out NativePoint point))
        {
            return false;
        }

        nint window = WindowFromPoint(point);
        return window != nint.Zero && OwnerProcess(window) == processId;
    }

    private static nint SetCursorHook(nint cursor) =>
        PointerBelongsToThisSession() ? SetCursor(cursor) : GetCursor();

    /// <summary>
    /// Capturing the mouse is only for the player's own press on the game. A macro click is a posted
    /// message (virtual pointer): no physical button is down, so Flash never grabs the real mouse for it,
    /// even when the player's pointer happens to rest over the game.
    /// </summary>
    private static nint SetCaptureHook(nint window) =>
        IsPhysicalButtonDown() && PointerBelongsToThisSession() ? SetCapture(window) : nint.Zero;

    private static bool IsPhysicalButtonDown() =>
        (GetAsyncKeyState(VirtualKeyLeftButton) & 0x8000) != 0 ||
        (GetAsyncKeyState(VirtualKeyRightButton) & 0x8000) != 0;

    private static bool ReleaseCaptureHook()
    {
        // Only release a capture this session owns: never the one the launcher holds while the player
        // is pressing one of its buttons.
        nint capture = GetCapture();
        return capture == nint.Zero || OwnerProcess(capture) != (uint)Environment.ProcessId || ReleaseCapture();
    }

    private static bool SetCursorPosHook(int x, int y) =>
        !PointerBelongsToThisSession() || SetCursorPos(x, y);

    private static uint OwnerProcess(nint window)
    {
        _ = GetWindowThreadProcessId(window, out uint processId);
        return processId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern nint SetCursor(nint cursor);

    [DllImport("user32.dll")]
    private static extern nint GetCursor();

    [DllImport("user32.dll")]
    private static extern nint SetCapture(nint window);

    [DllImport("user32.dll")]
    private static extern nint GetCapture();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    private static extern nint WindowFromPoint(NativePoint point);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
}

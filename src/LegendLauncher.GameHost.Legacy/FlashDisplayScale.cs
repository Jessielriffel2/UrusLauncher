using System.Globalization;
using System.Runtime.InteropServices;
using LegendLauncher.Core.GameDisplay;
using LegendLauncher.Infrastructure.Logging;

namespace LegendLauncher.GameHost.Legacy;

/// <summary>
/// Game zoom (<see cref="GameZoom"/>): Flash is made to believe its window is the physical size divided by the scale, and every
/// frame it shows is enlarged by the same scale, the way Windows scales an old program on a high-DPI screen or a browser zooms
/// a page. Flash keeps rendering in its "no scale" mode, so the game simply sees a smaller stage (for example 1504x620 instead
/// of 3760x1550 on a TV) and lays out its interface, battle and map for it.
/// <para>
/// Only the Flash OCX of this process changes, through the same import-table redirection used by
/// <see cref="FlashColorFilter"/> and <see cref="FlashPointerIsolation"/>:
/// the stage size comes from <c>IOleInPlaceObject::SetObjectRects</c> (its vtable entry is redirected) and the render buffer from
/// <c>GetClientRect</c>; the mouse arrives in the messages (translated by <see cref="FlashActiveXControl"/>) and through
/// <c>ScreenToClient</c>; frames reach the window through the drawing calls already owned by <see cref="FlashColorFilter"/>,
/// which ask <see cref="TryMapDestination"/> where to draw. Everything else (keyboard, sound, network) is untouched.
/// </para>
/// <para>
/// The scale only exists in the GDI window mode (<c>opaque</c>); with <c>direct</c> (Direct3D) or when any required hook is
/// missing the game stays exactly as before (scale 1).
/// </para>
/// </summary>
internal static partial class FlashDisplayScale
{
    // IUnknown (3) + IOleWindow (GetWindow, ContextSensitiveHelp) + InPlaceDeactivate + UIDeactivate, then SetObjectRects.
    private const int SetObjectRectsSlot = 7;
    private static readonly Guid InPlaceObjectInterface = new("00000113-0000-0000-C000-000000000046");

    private const int HalfTone = 4;
    private const int DefaultDpi = 96;

    private static readonly GameZoom.Reader s_channel = new();

    private static int s_installed;
    private static bool s_enabled;
    private static nint s_window;
    private static nint s_inPlaceObject;
    private static SetObjectRectsProc? s_originalSetObjectRects;
    private static SetObjectRectsProc? s_setObjectRectsHook;

    private static int s_choice = GameZoom.Automatic;
    private static double s_monitorScale = 1;
    private static double s_scale = 1;
    private static NativeRect s_physical;
    private static NativeRect s_logical;
    private static nint s_printDc;
    private static int s_reported;

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int SetObjectRectsProc(nint self, nint position, nint clip);

    /// <summary>Physical pixels per game pixel; 1 when the zoom is off or not available.</summary>
    internal static double Scale => Volatile.Read(ref s_scale);

    internal static bool IsActive => Scale > 1;

    /// <summary>The stage Flash currently believes it has (diagnostics/tests).</summary>
    internal static (int Width, int Height) LogicalSize => (s_logical.Width, s_logical.Height);

    /// <summary>
    /// Redirects the geometry of the Flash window. Must run after <see cref="FlashColorFilter.Install"/> (the frames have to be
    /// enlarged by its drawing hooks) and before <c>LoadMovie</c>. Failures only log: the game then keeps its original size.
    /// </summary>
    public static void Install(string flashOcxPath, nint flashWindow, object flashControl)
    {
        if (Interlocked.CompareExchange(ref s_installed, 1, 0) != 0)
        {
            return;
        }

        try
        {
            if (!FlashColorFilter.PresentsFrames)
            {
                DiagnosticLog.Current.WriteFailure(
                    "gamehost.display_scale",
                    "The Flash drawing calls are not redirected; the game zoom stays off.",
                    exception: null,
                    ("flashOcxPath", flashOcxPath));
                return;
            }

            nint module = ImportTablePatcher.FindModule(flashOcxPath);
            if (module == nint.Zero || flashWindow == nint.Zero)
            {
                DiagnosticLog.Current.WriteFailure(
                    "gamehost.display_scale",
                    "The Flash OCX module or window was not found; the game zoom stays off.",
                    exception: null,
                    ("flashOcxPath", flashOcxPath));
                return;
            }

            s_window = flashWindow;
            s_choice = s_channel.Current;
            s_monitorScale = ReadMonitorScale();
            int patched = PatchImports(module);
            bool vtable = HookSetObjectRects(flashControl);
            s_enabled = vtable && patched == RequiredImports;
            if (!s_enabled)
            {
                DiagnosticLog.Current.WriteFailure(
                    "gamehost.display_scale",
                    "Not every geometry call of the Flash OCX could be redirected; the game zoom stays off.",
                    exception: null,
                    ("patchedImports", patched.ToString(CultureInfo.InvariantCulture)),
                    ("setObjectRects", vtable.ToString(CultureInfo.InvariantCulture)));
            }
        }
        catch (Exception exception) when (
            exception is AccessViolationException or ArgumentException or InvalidOperationException or ExternalException or
                InvalidCastException)
        {
            s_enabled = false;
            DiagnosticLog.Current.WriteFailure(
                "gamehost.display_scale",
                "The game zoom could not be installed; the game keeps its original size.",
                exception,
                ("flashOcxPath", flashOcxPath));
        }
    }

    /// <summary>
    /// Called periodically (and after layout changes) with the control bounds inside its parent. Re-reads the launcher choice and
    /// the monitor scale and, when the result changes, gives Flash its new stage at once (no reload: the game just relayouts).
    /// </summary>
    public static void Refresh(int left, int top, int width, int height, bool force = false)
    {
        if (!s_enabled || s_inPlaceObject == nint.Zero || width <= 0 || height <= 0)
        {
            return;
        }

        int choice = s_channel.Current;
        double monitorScale = ReadMonitorScale();
        var physical = new NativeRect(left, top, left + width, top + height);
        double expected = GameZoom.ComputeScale(choice, width, height, monitorScale);
        bool changed = choice != s_choice ||
            Math.Abs(monitorScale - s_monitorScale) > 0.001 ||
            Math.Abs(expected - Scale) > 0.0001 ||
            !physical.Equals(s_physical);
        if (!changed && !force)
        {
            return;
        }

        s_choice = choice;
        s_monitorScale = monitorScale;
        nint buffer = Marshal.AllocHGlobal(2 * Marshal.SizeOf<NativeRect>());
        try
        {
            Marshal.StructureToPtr(physical, buffer, false);
            Marshal.StructureToPtr(physical, buffer + Marshal.SizeOf<NativeRect>(), false);
            _ = SetObjectRectsHook(s_inPlaceObject, buffer, buffer + Marshal.SizeOf<NativeRect>());
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        _ = InvalidateRect(s_window, nint.Zero, false);
    }

    private static bool HookSetObjectRects(object flashControl)
    {
        nint unknown = Marshal.GetIUnknownForObject(flashControl);
        try
        {
            Guid interfaceId = InPlaceObjectInterface;
            if (Marshal.QueryInterface(unknown, in interfaceId, out nint inPlaceObject) != 0 || inPlaceObject == nint.Zero)
            {
                return false;
            }

            // Kept (one reference) for the whole process: Refresh calls the hook with it.
            s_inPlaceObject = inPlaceObject;
            nint slot = Marshal.ReadIntPtr(inPlaceObject) + (SetObjectRectsSlot * nint.Size);
            s_setObjectRectsHook = SetObjectRectsHook;
            nint original = ImportTablePatcher.ReplacePointer(slot, Marshal.GetFunctionPointerForDelegate(s_setObjectRectsHook));
            s_originalSetObjectRects = Marshal.GetDelegateForFunctionPointer<SetObjectRectsProc>(original);
            return true;
        }
        finally
        {
            Marshal.Release(unknown);
        }
    }

    /// <summary>
    /// The container (AxHost) tells Flash where its window is: the stage size comes from here. The physical rectangle is
    /// remembered and Flash receives it divided by the scale; Flash then moves its window there (SetWindowPos), which
    /// <see cref="SetWindowPosHook"/> turns back into the exact physical rectangle.
    /// </summary>
    private static int SetObjectRectsHook(nint self, nint position, nint clip)
    {
        SetObjectRectsProc original = s_originalSetObjectRects!;
        if (!s_enabled || position == nint.Zero)
        {
            return original(self, position, clip);
        }

        try
        {
            NativeRect physical = Marshal.PtrToStructure<NativeRect>(position);
            NativeRect physicalClip = clip == nint.Zero ? physical : Marshal.PtrToStructure<NativeRect>(clip);
            double scale = GameZoom.ComputeScale(s_choice, physical.Width, physical.Height, s_monitorScale);
            s_physical = physical;
            if (scale <= 1)
            {
                s_logical = physical;
                Volatile.Write(ref s_scale, 1d);
                return original(self, position, clip);
            }

            NativeRect logical = ToLogical(physical, scale);
            NativeRect logicalClip = ToLogical(physicalClip, scale);
            s_logical = logical;
            Volatile.Write(ref s_scale, scale);
            ReportOnce(physical, logical, scale);

            int size = Marshal.SizeOf<NativeRect>();
            nint buffer = Marshal.AllocHGlobal(2 * size);
            try
            {
                Marshal.StructureToPtr(logical, buffer, false);
                Marshal.StructureToPtr(logicalClip, buffer + size, false);
                return original(self, buffer, clip == nint.Zero ? nint.Zero : buffer + size);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or ExternalException or OutOfMemoryException)
        {
            // Never let an exception cross back into the OCX: give Flash the original rectangle instead.
            Volatile.Write(ref s_scale, 1d);
            return original(self, position, clip);
        }
    }

    /// <summary>Same origin and size divided by the scale (at least one pixel).</summary>
    internal static NativeRect ToLogical(NativeRect physical, double scale)
    {
        int left = (int)Math.Round(physical.Left / scale);
        int top = (int)Math.Round(physical.Top / scale);
        int width = Math.Max(1, (int)Math.Round(physical.Width / scale));
        int height = Math.Max(1, (int)Math.Round(physical.Height / scale));
        return new NativeRect(left, top, left + width, top + height);
    }

    private static int ToPhysical(int value, double scale) => (int)Math.Round(value * scale);

    private static int ToLogicalCoordinate(int value, double scale) => (int)Math.Floor(value / scale);

    /// <summary>
    /// Mouse messages carry client coordinates of the physical window: Flash receives them divided by the scale. Returns the
    /// translated lParam, or the same value when the message has no client coordinates or the zoom is off.
    /// </summary>
    internal static nint TranslateMessageParameter(int message, nint lParam)
    {
        double scale = Scale;
        if (scale <= 1)
        {
            return lParam;
        }

        if (message == WindowMessageSize)
        {
            int width = (short)((long)lParam & 0xFFFF);
            int height = (short)(((long)lParam >> 16) & 0xFFFF);
            int logicalWidth = width == s_physical.Width ? s_logical.Width : Math.Max(1, (int)Math.Round(width / scale));
            int logicalHeight = height == s_physical.Height ? s_logical.Height : Math.Max(1, (int)Math.Round(height / scale));
            return MakeParameter(logicalWidth, logicalHeight);
        }

        if (!IsClientMouseMessage(message))
        {
            return lParam;
        }

        int x = (short)((long)lParam & 0xFFFF);
        int y = (short)(((long)lParam >> 16) & 0xFFFF);
        return MakeParameter(ToLogicalCoordinate(x, scale), ToLogicalCoordinate(y, scale));
    }

    internal static bool IsClientMouseMessage(int message) =>
        message is >= 0x0200 and <= 0x0209 or >= 0x020B and <= 0x020D or 0x02A1;

    private static nint MakeParameter(int low, int high) => (nint)(((high & 0xFFFF) << 16) | (low & 0xFFFF));

    /// <summary>
    /// While the window handles WM_PRINT/WM_PRINTCLIENT (PrintWindow, the eyes of the macro) Flash draws into the given device
    /// context: it gets the same enlargement as the screen, so captures keep the physical size the macro clicks on.
    /// </summary>
    internal static nint BeginPrint(nint deviceContext)
    {
        nint previous = s_printDc;
        s_printDc = deviceContext;
        return previous;
    }

    internal static void EndPrint(nint previous) => s_printDc = previous;

    /// <summary>
    /// For the drawing hooks: when <paramref name="deviceContext"/> is the Flash window (or a capture of it) and the zoom is on,
    /// the destination rectangle (in game pixels) becomes the physical one and the call has to stretch.
    /// </summary>
    internal static bool TryMapDestination(nint deviceContext, ref int x, ref int y, ref int width, ref int height)
    {
        double scale = Scale;
        if (scale <= 1 || !IsFlashDeviceContext(deviceContext))
        {
            return false;
        }

        int left = ToPhysical(x, scale);
        int top = ToPhysical(y, scale);
        int right = ToPhysical(x + width, scale);
        int bottom = ToPhysical(y + height, scale);
        x = left;
        y = top;
        width = right - left;
        height = bottom - top;
        return true;
    }

    /// <summary>Smooth enlargement (HALFTONE) of a frame onto the mapped destination; restores the previous mode.</summary>
    internal static bool Stretch(
        nint destination, int x, int y, int width, int height, nint source, int sourceX, int sourceY, int sourceWidth,
        int sourceHeight, uint rasterOperation)
    {
        int previous = SetStretchBltMode(destination, HalfTone);
        _ = SetBrushOrgEx(destination, 0, 0, nint.Zero);
        bool result = StretchBlt(
            destination, x, y, width, height, source, sourceX, sourceY, sourceWidth, sourceHeight, rasterOperation);
        if (previous != 0 && previous != HalfTone)
        {
            _ = SetStretchBltMode(destination, previous);
        }

        return result;
    }

    /// <summary>Selects HALFTONE before a StretchDIBits onto the window; returns the mode to restore.</summary>
    internal static int UseSmoothStretch(nint destination)
    {
        int previous = SetStretchBltMode(destination, HalfTone);
        _ = SetBrushOrgEx(destination, 0, 0, nint.Zero);
        return previous;
    }

    internal static void RestoreStretch(nint destination, int previous)
    {
        if (previous != 0 && previous != HalfTone)
        {
            _ = SetStretchBltMode(destination, previous);
        }
    }

    private static double ReadMonitorScale()
    {
        uint dpi = s_window == nint.Zero ? 0 : GetDpiForWindow(s_window);
        return (dpi == 0 ? DefaultDpi : dpi) / (double)DefaultDpi;
    }

    /// <summary>One diagnostic line per session with the sizes, so a player's log tells whether the zoom was applied.</summary>
    private static void ReportOnce(NativeRect physical, NativeRect logical, double scale)
    {
        if (Interlocked.CompareExchange(ref s_reported, 1, 0) != 0)
        {
            return;
        }

        DiagnosticLog.Current.WriteFailure(
            "gamehost.display_scale_active",
            "The game zoom is enlarging the Flash frames (informational).",
            exception: null,
            ("choice", s_choice.ToString(CultureInfo.InvariantCulture)),
            ("monitorScale", s_monitorScale.ToString("0.###", CultureInfo.InvariantCulture)),
            ("scale", scale.ToString("0.###", CultureInfo.InvariantCulture)),
            ("physical", $"{physical.Width}x{physical.Height}"),
            ("logical", $"{logical.Width}x{logical.Height}"));
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct NativeRect(int Left, int Top, int Right, int Bottom)
    {
        public int Width => Right - Left;

        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativePoint
    {
        public int X;
        public int Y;
    }
}

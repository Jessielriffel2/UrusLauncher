using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace LegendLauncher.App;

/// <summary>
/// Keeps maximized custom-chrome windows inside the monitor work area so the taskbar remains visible.
/// </summary>
internal static class BorderlessWindowWorkArea
{
    private const int GetMinMaxInfoMessage = 0x0024;
    internal const int SettingChangeMessage = 0x001A;
    internal const int DisplayChangeMessage = 0x007E;
    internal const int DpiChangedMessage = 0x02E0;
    private const uint MonitorDefaultToNearest = 0x00000002;
    private static readonly ConditionalWeakTable<Window, Registration> Registrations = new();

    public static void Attach(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        _ = Registrations.GetValue(window, static target => new Registration(target));
    }

    internal static MaximizedLayout CalculateMaximizedLayout(NativeRect monitor, NativeRect workArea) =>
        new(
            new NativePoint(workArea.Left - monitor.Left, workArea.Top - monitor.Top),
            new NativePoint(workArea.Right - workArea.Left, workArea.Bottom - workArea.Top));

    internal static NormalWindowLimits CalculateNormalWindowLimits(
        NativePoint workAreaSizePixels,
        uint dpi,
        double originalMinWidth,
        double originalMinHeight,
        double originalMaxWidth,
        double originalMaxHeight)
    {
        double availableWidth = PixelsToDip(Math.Max(0, workAreaSizePixels.X), dpi);
        double availableHeight = PixelsToDip(Math.Max(0, workAreaSizePixels.Y), dpi);
        double maxWidth = Math.Min(originalMaxWidth, availableWidth);
        double maxHeight = Math.Min(originalMaxHeight, availableHeight);
        return new NormalWindowLimits(
            Math.Min(originalMinWidth, maxWidth),
            Math.Min(originalMinHeight, maxHeight),
            maxWidth,
            maxHeight);
    }

    internal static double PixelsToDip(int pixels, uint dpi)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pixels);
        uint effectiveDpi = dpi == 0 ? 96u : dpi;
        return pixels * 96d / effectiveDpi;
    }

    internal static bool RequiresNormalLimitsRefresh(int message) =>
        message is SettingChangeMessage or DisplayChangeMessage or DpiChangedMessage;

    private sealed class Registration
    {
        private readonly Window _window;
        private readonly double _originalMinWidth;
        private readonly double _originalMinHeight;
        private readonly double _originalMaxWidth;
        private readonly double _originalMaxHeight;
        private HwndSource? _source;
        private bool _updatingNormalLimits;
        private bool _normalLimitsRefreshQueued;
        private bool _isClosed;

        public Registration(Window window)
        {
            _window = window;
            _originalMinWidth = window.MinWidth;
            _originalMinHeight = window.MinHeight;
            _originalMaxWidth = window.MaxWidth;
            _originalMaxHeight = window.MaxHeight;
            _window.SourceInitialized += WindowOnSourceInitialized;
            _window.LocationChanged += WindowOnLocationChanged;
            _window.StateChanged += WindowOnStateChanged;
            _window.Closed += WindowOnClosed;

            nint existingHandle = new WindowInteropHelper(_window).Handle;
            if (existingHandle != nint.Zero)
            {
                AttachSource(existingHandle);
                UpdateNormalWindowLimits(existingHandle);
            }
        }

        private void WindowOnSourceInitialized(object? sender, EventArgs eventArgs)
        {
            nint handle = new WindowInteropHelper(_window).Handle;
            AttachSource(handle);
            UpdateNormalWindowLimits(handle);
        }

        private void WindowOnLocationChanged(object? sender, EventArgs eventArgs)
        {
            UpdateNormalWindowLimits();
        }

        private void WindowOnStateChanged(object? sender, EventArgs eventArgs)
        {
            UpdateNormalWindowLimits();
        }

        private void WindowOnClosed(object? sender, EventArgs eventArgs)
        {
            _isClosed = true;
            _source?.RemoveHook(WindowProcedure);
            _source = null;
            _window.SourceInitialized -= WindowOnSourceInitialized;
            _window.LocationChanged -= WindowOnLocationChanged;
            _window.StateChanged -= WindowOnStateChanged;
            _window.Closed -= WindowOnClosed;
            Registrations.Remove(_window);
        }

        private void QueueNormalWindowLimitsRefresh()
        {
            if (_isClosed ||
                _normalLimitsRefreshQueued ||
                _window.Dispatcher.HasShutdownStarted ||
                _window.Dispatcher.HasShutdownFinished)
            {
                return;
            }

            _normalLimitsRefreshQueued = true;
            try
            {
                _ = _window.Dispatcher.BeginInvoke(
                    DispatcherPriority.Background,
                    (Action)RefreshQueuedNormalWindowLimits);
            }
            catch (InvalidOperationException)
            {
                _normalLimitsRefreshQueued = false;
            }
        }

        private void RefreshQueuedNormalWindowLimits()
        {
            _normalLimitsRefreshQueued = false;
            if (!_isClosed)
            {
                UpdateNormalWindowLimits();
            }
        }

        private void UpdateNormalWindowLimits()
        {
            UpdateNormalWindowLimits(new WindowInteropHelper(_window).Handle);
        }

        private void UpdateNormalWindowLimits(nint handle)
        {
            if (_updatingNormalLimits ||
                handle == nint.Zero ||
                _window.WindowState == WindowState.Minimized ||
                !TryGetMonitorInfo(handle, out MonitorInfo monitorInfo))
            {
                return;
            }

            uint dpi = GetDpiForWindow(handle);
            var workAreaSize = new NativePoint(
                monitorInfo.WorkArea.Right - monitorInfo.WorkArea.Left,
                monitorInfo.WorkArea.Bottom - monitorInfo.WorkArea.Top);
            NormalWindowLimits limits = CalculateNormalWindowLimits(
                workAreaSize,
                dpi,
                _originalMinWidth,
                _originalMinHeight,
                _originalMaxWidth,
                _originalMaxHeight);

            _updatingNormalLimits = true;
            try
            {
                ApplyWidthLimits(limits.MinWidth, limits.MaxWidth);
                ApplyHeightLimits(limits.MinHeight, limits.MaxHeight);
                if (_window.WindowState == WindowState.Normal)
                {
                    if (double.IsFinite(_window.Width) && _window.Width > limits.MaxWidth)
                    {
                        _window.Width = limits.MaxWidth;
                    }

                    if (double.IsFinite(_window.Height) && _window.Height > limits.MaxHeight)
                    {
                        _window.Height = limits.MaxHeight;
                    }
                }
            }
            finally
            {
                _updatingNormalLimits = false;
            }
        }

        private void ApplyWidthLimits(double minWidth, double maxWidth)
        {
            if (minWidth < _window.MinWidth)
            {
                _window.MinWidth = minWidth;
            }

            if (maxWidth > _window.MaxWidth)
            {
                _window.MaxWidth = maxWidth;
            }

            _window.MaxWidth = maxWidth;
            _window.MinWidth = minWidth;
        }

        private void ApplyHeightLimits(double minHeight, double maxHeight)
        {
            if (minHeight < _window.MinHeight)
            {
                _window.MinHeight = minHeight;
            }

            if (maxHeight > _window.MaxHeight)
            {
                _window.MaxHeight = maxHeight;
            }

            _window.MaxHeight = maxHeight;
            _window.MinHeight = minHeight;
        }

        private void AttachSource(nint handle)
        {
            if (_source is not null || handle == nint.Zero)
            {
                return;
            }

            _source = HwndSource.FromHwnd(handle);
            _source?.AddHook(WindowProcedure);
        }

        private nint WindowProcedure(
            nint windowHandle,
            int message,
            nint wordParameter,
            nint longParameter,
            ref bool handled)
        {
            if (RequiresNormalLimitsRefresh(message))
            {
                QueueNormalWindowLimitsRefresh();
                return nint.Zero;
            }

            if (message != GetMinMaxInfoMessage || longParameter == nint.Zero)
            {
                return nint.Zero;
            }

            if (!TryGetMonitorInfo(windowHandle, out MonitorInfo monitorInfo))
            {
                return nint.Zero;
            }

            MinMaxInfo minMaxInfo = Marshal.PtrToStructure<MinMaxInfo>(longParameter);
            MaximizedLayout layout = CalculateMaximizedLayout(
                monitorInfo.Monitor,
                monitorInfo.WorkArea);
            minMaxInfo.MaxPosition = layout.Position;
            minMaxInfo.MaxSize = layout.Size;
            Marshal.StructureToPtr(minMaxInfo, longParameter, false);
            handled = true;
            return nint.Zero;
        }

        private static bool TryGetMonitorInfo(nint windowHandle, out MonitorInfo monitorInfo)
        {
            nint monitorHandle = MonitorFromWindow(windowHandle, MonitorDefaultToNearest);
            monitorInfo = new MonitorInfo
            {
                Size = Marshal.SizeOf<MonitorInfo>(),
            };
            return monitorHandle != nint.Zero && GetMonitorInfo(monitorHandle, ref monitorInfo);
        }
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitorHandle, ref MonitorInfo monitorInfo);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint windowHandle, uint flags);

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativePoint
    {
        public NativePoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X;

        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeRect
    {
        public NativeRect(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public int Left;

        public int Top;

        public int Right;

        public int Bottom;
    }

    internal readonly record struct MaximizedLayout(NativePoint Position, NativePoint Size);

    internal readonly record struct NormalWindowLimits(
        double MinWidth,
        double MinHeight,
        double MaxWidth,
        double MaxHeight);

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public NativePoint Reserved;
        public NativePoint MaxSize;
        public NativePoint MaxPosition;
        public NativePoint MinTrackSize;
        public NativePoint MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect WorkArea;
        public uint Flags;
    }
}

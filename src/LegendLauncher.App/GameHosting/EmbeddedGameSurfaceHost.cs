using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace LegendLauncher.App.GameHosting;

/// <summary>
/// WPF host that owns a same-process proxy HWND and places an isolated GameHost below that proxy.
/// </summary>
internal sealed class EmbeddedGameSurfaceHost : HwndHost
{
    private readonly GameWindowAttachment _attachment;
    private nint _proxyWindow;
    private NativeClientSize _lastSyncedSize;
    private bool _layoutHooked;
    private bool _syncQueued;

    public EmbeddedGameSurfaceHost(GameWindowAttachment attachment)
    {
        _attachment = attachment ?? throw new ArgumentNullException(nameof(attachment));
        Focusable = true;
    }

    public bool FocusGameWindow() =>
        _proxyWindow != nint.Zero && _attachment.FocusWithin(_proxyWindow);

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        if (_proxyWindow != nint.Zero)
        {
            throw new InvalidOperationException("The embedded game surface already owns a proxy window.");
        }

        nint proxyWindow = NativeWindowMethods.CreateProxyWindow(hwndParent.Handle);
        try
        {
            _attachment.AttachTo(proxyWindow);
            _proxyWindow = proxyWindow;
            HookLayoutSync();
            RequestSyncToProxy();
            return new HandleRef(this, proxyWindow);
        }
        catch
        {
            if (!_attachment.IsCurrentParent(proxyWindow))
            {
                NativeWindowMethods.DestroyProxyWindow(proxyWindow);
            }

            throw;
        }
    }

    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        UnhookLayoutSync();
        nint proxyWindow = hwnd.Handle;
        if (proxyWindow == nint.Zero)
        {
            return;
        }

        if (!_attachment.ParkIfParent(proxyWindow, GameHostParkingSurface.Handle))
        {
            _ = _attachment.DetachIfParent(proxyWindow);
        }

        NativeWindowMethods.DestroyProxyWindow(proxyWindow);
        if (_proxyWindow == proxyWindow)
        {
            _proxyWindow = nint.Zero;
        }

        _lastSyncedSize = default;
    }

    protected override void OnWindowPositionChanged(Rect rcBoundingBox)
    {
        base.OnWindowPositionChanged(rcBoundingBox);
        SyncGameWindowToProxy(force: true);
    }

    protected override bool TabIntoCore(TraversalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return FocusGameWindow();
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        _ = FocusGameWindow();
    }

    private void HookLayoutSync()
    {
        if (_layoutHooked)
        {
            return;
        }

        LayoutUpdated += OnLayoutUpdated;
        SizeChanged += OnHostSizeChanged;
        _layoutHooked = true;
    }

    private void UnhookLayoutSync()
    {
        if (!_layoutHooked)
        {
            return;
        }

        LayoutUpdated -= OnLayoutUpdated;
        SizeChanged -= OnHostSizeChanged;
        _layoutHooked = false;
        _syncQueued = false;
    }

    private void OnLayoutUpdated(object? sender, EventArgs e) => RequestSyncToProxy();

    private void OnHostSizeChanged(object sender, SizeChangedEventArgs e) => RequestSyncToProxy();

    private void RequestSyncToProxy()
    {
        if (_proxyWindow == nint.Zero || _syncQueued)
        {
            return;
        }

        _syncQueued = true;
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            _syncQueued = false;
            SyncGameWindowToProxy(force: false);
        });
    }

    private void SyncGameWindowToProxy(bool force)
    {
        if (_proxyWindow == nint.Zero)
        {
            return;
        }

        NativeClientSize size = NativeWindowMethods.GetClientSize(_proxyWindow);
        if (!force &&
            size.Width == _lastSyncedSize.Width &&
            size.Height == _lastSyncedSize.Height)
        {
            return;
        }

        _lastSyncedSize = size;
        _attachment.ResizeTo(_proxyWindow);
    }
}

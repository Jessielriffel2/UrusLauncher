using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace LegendLauncher.App.GameHosting;

/// <summary>
/// WPF host that owns a same-process proxy HWND and places an isolated GameHost below that proxy.
/// </summary>
internal sealed class EmbeddedGameSurfaceHost : HwndHost
{
    private readonly GameWindowAttachment _attachment;
    private nint _proxyWindow;

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
        nint proxyWindow = hwnd.Handle;
        if (proxyWindow == nint.Zero)
        {
            return;
        }

        _ = _attachment.DetachIfParent(proxyWindow);
        NativeWindowMethods.DestroyProxyWindow(proxyWindow);
        if (_proxyWindow == proxyWindow)
        {
            _proxyWindow = nint.Zero;
        }
    }

    protected override void OnWindowPositionChanged(Rect rcBoundingBox)
    {
        base.OnWindowPositionChanged(rcBoundingBox);
        if (_proxyWindow != nint.Zero)
        {
            _attachment.ResizeTo(_proxyWindow);
        }
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
}

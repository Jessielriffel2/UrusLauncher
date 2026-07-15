namespace LegendLauncher.App.GameHosting;

/// <summary>
/// Owns the reversible relationship between one isolated GameHost HWND and launcher proxy HWNDs.
/// It never owns or destroys the GameHost window.
/// </summary>
internal sealed class GameWindowAttachment
{
    private readonly nint _gameWindow;
    private readonly int _expectedProcessId;
    private readonly uint _standaloneStyle;

    public GameWindowAttachment(nint gameWindow, int expectedProcessId)
    {
        bool exists = NativeWindowMethods.IsWindowHandle(gameWindow);
        uint actualProcessId = exists
            ? NativeWindowMethods.GetWindowProcessId(gameWindow)
            : 0;
        ValidateExternalWindowIdentity(
            gameWindow,
            expectedProcessId,
            exists,
            actualProcessId);

        _gameWindow = gameWindow;
        _expectedProcessId = expectedProcessId;
        _standaloneStyle = NativeWindowMethods.GetWindowStyle(gameWindow);
    }

    public nint GameWindow => _gameWindow;

    public int ExpectedProcessId => _expectedProcessId;

    public void AttachTo(nint proxyWindow)
    {
        ValidateExternalWindow();
        ValidateProxyWindow(proxyWindow);

        nint previousParent = NativeWindowMethods.GetParentWindow(_gameWindow);
        uint previousStyle = NativeWindowMethods.GetWindowStyle(_gameWindow);
        NativeWindowMethods.HideWindow(_gameWindow);

        try
        {
            NativeWindowMethods.SetWindowStyle(
                _gameWindow,
                NativeWindowMethods.CalculateEmbeddedStyle(previousStyle));
            _ = NativeWindowMethods.SetParentWindow(_gameWindow, proxyWindow);
            ResizeTo(proxyWindow, focus: true);
        }
        catch
        {
            RollBackAttachment(proxyWindow, previousParent, previousStyle);
            throw;
        }
    }

    public void ResizeTo(nint proxyWindow) => ResizeTo(proxyWindow, focus: false);

    public bool FocusWithin(nint proxyWindow)
    {
        if (!IsCurrentParent(proxyWindow))
        {
            return false;
        }

        NativeWindowMethods.ShowWindowAndFocus(_gameWindow);
        return true;
    }

    public bool DetachIfParent(nint proxyWindow)
    {
        if (!NativeWindowMethods.IsWindowHandle(_gameWindow))
        {
            return false;
        }

        nint currentParent = NativeWindowMethods.GetParentWindow(_gameWindow);
        if (!ShouldDetachFromProxy(proxyWindow, currentParent))
        {
            return false;
        }

        NativeWindowMethods.HideWindow(_gameWindow);
        _ = NativeWindowMethods.SetParentWindow(_gameWindow, nint.Zero);
        NativeWindowMethods.SetWindowStyle(_gameWindow, _standaloneStyle);
        return true;
    }

    public bool IsCurrentParent(nint proxyWindow) =>
        NativeWindowMethods.IsWindowHandle(_gameWindow) &&
        ShouldDetachFromProxy(
            proxyWindow,
            NativeWindowMethods.GetParentWindow(_gameWindow));

    internal static void ValidateExternalWindowIdentity(
        nint gameWindow,
        int expectedProcessId,
        bool windowExists,
        uint actualProcessId)
    {
        if (gameWindow == nint.Zero)
        {
            throw new ArgumentException("A GameHost window handle is required.", nameof(gameWindow));
        }

        if (expectedProcessId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedProcessId),
                "A positive GameHost process identifier is required.");
        }

        if (!windowExists)
        {
            throw new ArgumentException("The GameHost window handle is no longer valid.", nameof(gameWindow));
        }

        if (actualProcessId == 0 || actualProcessId != unchecked((uint)expectedProcessId))
        {
            throw new InvalidOperationException(
                "The GameHost window does not belong to the expected isolated process.");
        }
    }

    internal static bool ShouldDetachFromProxy(nint proxyWindow, nint currentParent) =>
        proxyWindow != nint.Zero && currentParent == proxyWindow;

    internal static void ValidateProxyWindowIdentity(
        nint proxyWindow,
        bool windowExists,
        uint actualProcessId,
        uint launcherProcessId)
    {
        if (proxyWindow == nint.Zero ||
            !windowExists ||
            launcherProcessId == 0 ||
            actualProcessId != launcherProcessId)
        {
            throw new ArgumentException(
                "The proxy window must belong to the current launcher process.",
                nameof(proxyWindow));
        }
    }

    private void ValidateExternalWindow()
    {
        bool exists = NativeWindowMethods.IsWindowHandle(_gameWindow);
        uint actualProcessId = exists
            ? NativeWindowMethods.GetWindowProcessId(_gameWindow)
            : 0;
        ValidateExternalWindowIdentity(
            _gameWindow,
            _expectedProcessId,
            exists,
            actualProcessId);
    }

    private static void ValidateProxyWindow(nint proxyWindow)
    {
        bool exists = NativeWindowMethods.IsWindowHandle(proxyWindow);
        uint actualProcessId = exists
            ? NativeWindowMethods.GetWindowProcessId(proxyWindow)
            : 0;
        ValidateProxyWindowIdentity(
            proxyWindow,
            exists,
            actualProcessId,
            unchecked((uint)Environment.ProcessId));
    }

    private void ResizeTo(nint proxyWindow, bool focus)
    {
        if (!IsCurrentParent(proxyWindow))
        {
            return;
        }

        NativeClientSize clientSize = NativeWindowMethods.GetClientSize(proxyWindow);
        if (!clientSize.HasArea)
        {
            NativeWindowMethods.HideWindow(_gameWindow);
            return;
        }

        NativeWindowMethods.ResizeWindow(_gameWindow, clientSize);
        if (focus)
        {
            NativeWindowMethods.ShowWindowAndFocus(_gameWindow);
        }
        else
        {
            NativeWindowMethods.ShowWindow(_gameWindow);
        }
    }

    private void RollBackAttachment(
        nint attemptedProxy,
        nint previousParent,
        uint previousStyle)
    {
        try
        {
            NativeWindowMethods.HideWindow(_gameWindow);
            if (NativeWindowMethods.GetParentWindow(_gameWindow) == attemptedProxy)
            {
                _ = NativeWindowMethods.SetParentWindow(_gameWindow, previousParent);
            }

            NativeWindowMethods.SetWindowStyle(_gameWindow, previousStyle);
            if (previousParent != nint.Zero &&
                NativeWindowMethods.GetParentWindow(_gameWindow) == previousParent)
            {
                ResizeTo(previousParent, focus: false);
            }
        }
        catch
        {
            // Preserve the original attachment failure. The GameHost remains hidden instead of
            // exposing an unmanaged top-level compatibility window.
        }
    }
}

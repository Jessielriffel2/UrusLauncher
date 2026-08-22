namespace LegendLauncher.App.GameHosting;

/// <summary>
/// Owns one hidden launcher HWND used to keep live GameHost windows as children
/// while they are outside the visible workspace grid.
/// </summary>
internal static class GameHostParkingSurface
{
    private static readonly object Gate = new();
    private static nint _handle;

    public static nint Handle
    {
        get
        {
            lock (Gate)
            {
                if (_handle == nint.Zero || !NativeWindowMethods.IsWindowHandle(_handle))
                {
                    _handle = NativeWindowMethods.CreateHiddenParkingWindow();
                }

                return _handle;
            }
        }
    }
}

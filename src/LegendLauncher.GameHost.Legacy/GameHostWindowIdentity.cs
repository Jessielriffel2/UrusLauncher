using System.Runtime.InteropServices;

namespace LegendLauncher.GameHost.Legacy;

internal static class GameHostWindowIdentity
{
    public static void EnsureOwnedByProcess(nint nativeWindowHandle, int expectedProcessId)
    {
        if (nativeWindowHandle == nint.Zero ||
            expectedProcessId <= 0 ||
            GetWindowThreadProcessId(nativeWindowHandle, out uint actualProcessId) == 0 ||
            actualProcessId != (uint)expectedProcessId)
        {
            throw new InvalidOperationException(
                "The isolated GameHost returned an invalid native game window.");
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(
        nint windowHandle,
        out uint processId);
}

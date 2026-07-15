using System.IO.Pipes;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace LegendLauncher.GameHost.Legacy;

internal static class NamedPipePeerProcess
{
    public static void EnsureClientIs(NamedPipeServerStream pipe, int expectedProcessId)
    {
        ArgumentNullException.ThrowIfNull(pipe);
        if (expectedProcessId <= 0 ||
            !GetNamedPipeClientProcessId(pipe.SafePipeHandle, out uint actualProcessId) ||
            actualProcessId != (uint)expectedProcessId)
        {
            throw new UnauthorizedAccessException("The named-pipe client identity is invalid.");
        }
    }

    public static void EnsureServerIs(NamedPipeClientStream pipe, int expectedProcessId)
    {
        ArgumentNullException.ThrowIfNull(pipe);
        if (expectedProcessId <= 0 ||
            !GetNamedPipeServerProcessId(pipe.SafePipeHandle, out uint actualProcessId) ||
            actualProcessId != (uint)expectedProcessId)
        {
            throw new UnauthorizedAccessException("The named-pipe server identity is invalid.");
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(
        SafePipeHandle pipe,
        out uint clientProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeServerProcessId(
        SafePipeHandle pipe,
        out uint serverProcessId);
}

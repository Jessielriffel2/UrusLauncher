using System.ComponentModel;
using System.Runtime.InteropServices;

namespace LegendLauncher.GameHost.Legacy;

internal sealed class RegistrationFreeActivationContext : IDisposable
{
    private const uint AssemblyDirectoryValid = 0x00000004;
    private static readonly nint InvalidHandleValue = new(-1);

    private readonly nint _handle;
    private readonly nuint _cookie;
    private int _disposed;

    private RegistrationFreeActivationContext(nint handle, nuint cookie)
    {
        _handle = handle;
        _cookie = cookie;
    }

    public static RegistrationFreeActivationContext Activate(LegacyRuntimeAssets assets)
    {
        ArgumentNullException.ThrowIfNull(assets);
        if (!assets.IsComplete)
        {
            throw new InvalidOperationException("The registration-free Flash runtime is incomplete.");
        }

        var context = new ActCtx
        {
            Size = (uint)Marshal.SizeOf<ActCtx>(),
            Flags = AssemblyDirectoryValid,
            Source = assets.ManifestPath,
            AssemblyDirectory = assets.RuntimeRoot,
        };

        nint handle = CreateActCtx(ref context);
        if (handle == InvalidHandleValue)
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "The registration-free Flash activation context could not be created.");
        }

        if (!ActivateActCtx(handle, out nuint cookie))
        {
            int error = Marshal.GetLastWin32Error();
            ReleaseActCtx(handle);
            throw new Win32Exception(
                error,
                "The registration-free Flash activation context could not be activated.");
        }

        return new RegistrationFreeActivationContext(handle, cookie);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _ = DeactivateActCtx(0, _cookie);
        ReleaseActCtx(_handle);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ActCtx
    {
        public uint Size;
        public uint Flags;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string Source;

        public ushort ProcessorArchitecture;
        public ushort LanguageId;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? AssemblyDirectory;

        public nint ResourceName;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? ApplicationName;

        public nint Module;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateActCtxW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateActCtx(ref ActCtx context);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ActivateActCtx(nint context, out nuint cookie);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeactivateActCtx(uint flags, nuint cookie);

    [DllImport("kernel32.dll")]
    private static extern void ReleaseActCtx(nint context);
}

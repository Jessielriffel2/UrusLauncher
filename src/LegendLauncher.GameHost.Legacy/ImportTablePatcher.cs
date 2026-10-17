using System.Runtime.InteropServices;

namespace LegendLauncher.GameHost.Legacy;

/// <summary>
/// Redirects entries of the import table of ONE loaded module (the Flash OCX): only that module's calls change, never the system.
/// Used by <see cref="FlashPointerIsolation"/> (mouse) and <see cref="FlashColorFilter"/> (color saturation).
/// </summary>
internal static class ImportTablePatcher
{
    private const uint PageReadWrite = 0x04;

    /// <summary>Handle of the loaded module (full path first, then the file name); zero when it is not loaded.</summary>
    public static nint FindModule(string path)
    {
        nint module = GetModuleHandleW(path);
        return module != nint.Zero ? module : GetModuleHandleW(Path.GetFileName(path));
    }

    /// <summary>Replaces one entry of the module import table. Returns 1 when patched, 0 when absent.</summary>
    public static int Patch(nint module, string library, string function, nint replacement)
    {
        int peHeader = Marshal.ReadInt32(module, 0x3C);
        if (Marshal.ReadInt32(module, peHeader) != 0x00004550)
        {
            return 0;
        }

        nint optionalHeader = module + peHeader + 24;
        if (Marshal.ReadInt16(optionalHeader) != 0x20B)
        {
            return 0; // only the 64-bit OCX is shipped
        }

        // DataDirectory[1] (imports) of an IMAGE_OPTIONAL_HEADER64.
        int importDirectory = Marshal.ReadInt32(optionalHeader, 112 + 8);
        if (importDirectory == 0)
        {
            return 0;
        }

        for (nint descriptor = module + importDirectory; ; descriptor += 20)
        {
            int originalThunks = Marshal.ReadInt32(descriptor, 0);
            int nameRva = Marshal.ReadInt32(descriptor, 12);
            int thunks = Marshal.ReadInt32(descriptor, 16);
            if (nameRva == 0)
            {
                return 0;
            }

            string? name = Marshal.PtrToStringAnsi(module + nameRva);
            if (!string.Equals(name, library, StringComparison.OrdinalIgnoreCase) || originalThunks == 0)
            {
                continue;
            }

            for (int index = 0; ; index++)
            {
                long entry = Marshal.ReadInt64(module + originalThunks + (index * 8));
                if (entry == 0)
                {
                    break;
                }

                if (entry < 0)
                {
                    continue; // imported by ordinal
                }

                string? imported = Marshal.PtrToStringAnsi(module + (int)(entry & 0x7FFFFFFF) + 2);
                if (!string.Equals(imported, function, StringComparison.Ordinal))
                {
                    continue;
                }

                nint slot = module + thunks + (index * 8);
                if (!VirtualProtect(slot, 8, PageReadWrite, out uint previous))
                {
                    throw new ExternalException($"VirtualProtect failed for {function}.", Marshal.GetLastWin32Error());
                }

                Marshal.WriteIntPtr(slot, replacement);
                _ = VirtualProtect(slot, 8, previous, out _);
                return 1;
            }
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint GetModuleHandleW(string moduleName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool VirtualProtect(nint address, nuint size, uint newProtect, out uint oldProtect);
}

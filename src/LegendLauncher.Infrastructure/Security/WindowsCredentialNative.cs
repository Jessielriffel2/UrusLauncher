using System.Runtime.InteropServices;

namespace LegendLauncher.Infrastructure.Security;

internal static class WindowsCredentialNative
{
    internal const uint TypeGeneric = 1;
    internal const uint PersistLocalMachine = 2;
    internal const int ErrorNotFound = 1168;
    internal const int MaximumCredentialBlobSize = 5 * 512;

    [DllImport("Advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CredWrite(
        ref NativeCredential credential,
        uint flags);

    [DllImport("Advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CredRead(
        string target,
        uint type,
        uint flags,
        out IntPtr credential);

    [DllImport("Advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CredDelete(
        string target,
        uint type,
        uint flags);

    [DllImport("Advapi32.dll", EntryPoint = "CredFree")]
    internal static extern void CredFree(IntPtr buffer);

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeCredential
    {
        internal uint Flags;
        internal uint Type;
        internal IntPtr TargetName;
        internal IntPtr Comment;
        internal NativeFileTime LastWritten;
        internal uint CredentialBlobSize;
        internal IntPtr CredentialBlob;
        internal uint Persist;
        internal uint AttributeCount;
        internal IntPtr Attributes;
        internal IntPtr TargetAlias;
        internal IntPtr UserName;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeFileTime
    {
        internal uint LowDateTime;
        internal uint HighDateTime;
    }
}

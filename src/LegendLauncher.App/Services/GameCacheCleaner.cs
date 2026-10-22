using System.ComponentModel;
using System.Runtime.InteropServices;

namespace LegendLauncher.App.Services;

/// <summary>Outcome of <see cref="GameCacheCleaner.Clear"/>: entries removed, entries that could not be removed and the bytes freed.</summary>
internal readonly record struct GameCacheCleanResult(int Deleted, int Failed, long Bytes);

/// <summary>
/// "Clear game cache": the Flash ActiveX of the GameHost keeps every game file in the Windows (WinINet) cache of the current
/// user. A broken or stale copy there survives game updates (players reported windows that never open, fixed by deleting the
/// Internet temporary files by hand). This removes only the entries whose address passes <c>isGameUrl</c>; the rest of the
/// Windows cache is untouched. Entries are collected first and deleted after the enumeration is closed.
/// </summary>
internal static class GameCacheCleaner
{
    private const int ErrorInsufficientBuffer = 122;
    private const int ErrorNoMoreItems = 259;
    private const int InitialBufferSize = 16 * 1024;

    public static GameCacheCleanResult Clear(Func<Uri, bool> isGameUrl)
    {
        ArgumentNullException.ThrowIfNull(isGameUrl);
        if (!OperatingSystem.IsWindows())
        {
            return default;
        }

        var matches = new List<(string Url, long Size)>();
        int bufferSize = InitialBufferSize;
        IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
        IntPtr find = IntPtr.Zero;
        try
        {
            int size = bufferSize;
            find = FindFirstUrlCacheEntryW(null, buffer, ref size);
            if (find == IntPtr.Zero && Marshal.GetLastWin32Error() == ErrorInsufficientBuffer)
            {
                buffer = Grow(buffer, ref bufferSize, size);
                size = bufferSize;
                find = FindFirstUrlCacheEntryW(null, buffer, ref size);
            }

            if (find == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                return error == ErrorNoMoreItems ? default : throw new Win32Exception(error);
            }

            while (true)
            {
                Collect(buffer, isGameUrl, matches);
                size = bufferSize;
                if (FindNextUrlCacheEntryW(find, buffer, ref size))
                {
                    continue;
                }

                int error = Marshal.GetLastWin32Error();
                if (error == ErrorInsufficientBuffer)
                {
                    buffer = Grow(buffer, ref bufferSize, size);
                    size = bufferSize;
                    if (FindNextUrlCacheEntryW(find, buffer, ref size))
                    {
                        continue;
                    }

                    error = Marshal.GetLastWin32Error();
                }

                if (error == ErrorNoMoreItems)
                {
                    break;
                }

                throw new Win32Exception(error);
            }
        }
        finally
        {
            if (find != IntPtr.Zero)
            {
                FindCloseUrlCache(find);
            }

            Marshal.FreeHGlobal(buffer);
        }

        int deleted = 0;
        int failed = 0;
        long bytes = 0;
        foreach ((string url, long entrySize) in matches)
        {
            if (DeleteUrlCacheEntryW(url))
            {
                deleted++;
                bytes += entrySize;
            }
            else
            {
                failed++;
            }
        }

        return new GameCacheCleanResult(deleted, failed, bytes);
    }

    private static void Collect(IntPtr buffer, Func<Uri, bool> isGameUrl, List<(string Url, long Size)> matches)
    {
        var entry = Marshal.PtrToStructure<InternetCacheEntryInfo>(buffer);
        string? url = entry.SourceUrlName == IntPtr.Zero ? null : Marshal.PtrToStringUni(entry.SourceUrlName);
        if (url is not null
            && Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && isGameUrl(uri))
        {
            matches.Add((url, ((long)entry.SizeHigh << 32) | entry.SizeLow));
        }
    }

    private static IntPtr Grow(IntPtr buffer, ref int bufferSize, int needed)
    {
        bufferSize = Math.Max(needed, bufferSize * 2);
        return Marshal.ReAllocHGlobal(buffer, bufferSize);
    }

    // INTERNET_CACHE_ENTRY_INFOW (wininet.h); only the leading fields are read.
    [StructLayout(LayoutKind.Sequential)]
    private struct InternetCacheEntryInfo
    {
        public uint StructSize;
        public IntPtr SourceUrlName;
        public IntPtr LocalFileName;
        public uint CacheEntryType;
        public uint UseCount;
        public uint HitRate;
        public uint SizeLow;
        public uint SizeHigh;
    }

    [DllImport("wininet.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindFirstUrlCacheEntryW(string? urlSearchPattern, IntPtr firstCacheEntryInfo, ref int cacheEntryInfoSize);

    [DllImport("wininet.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FindNextUrlCacheEntryW(IntPtr enumHandle, IntPtr nextCacheEntryInfo, ref int cacheEntryInfoSize);

    [DllImport("wininet.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FindCloseUrlCache(IntPtr enumHandle);

    [DllImport("wininet.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteUrlCacheEntryW(string urlName);
}

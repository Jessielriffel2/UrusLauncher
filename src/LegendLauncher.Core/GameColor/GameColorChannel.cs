using System.IO.MemoryMappedFiles;

namespace LegendLauncher.Core.GameColor;

/// <summary>
/// The launcher's color saturation slider, shared with every isolated GameHost of the user's session through a tiny named
/// memory block (session-local "Local\" namespace). The launcher writes; the hosts read on every frame they present.
/// 100 = original colors (the filter is off), 0 = black and white.
/// </summary>
public static class GameColorChannel
{
    public const int Original = 100;

    public const string MapName = @"Local\UrusLauncher.GameColor.v1";

    private const int Size = 8;

    // Written next to the value: a block that exists but was never written by the launcher reads as "original colors".
    private const int Ready = 0x31435255; // "URC1"

    public static int Clamp(int saturation) => Math.Clamp(saturation, 0, Original);

    /// <summary>The launcher side: creates (or reopens) the block and keeps it alive while the launcher runs.</summary>
    public sealed class Writer : IDisposable
    {
        private readonly MemoryMappedFile? _map;
        private readonly MemoryMappedViewAccessor? _view;

        public Writer(string mapName = MapName)
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            try
            {
                _map = MemoryMappedFile.CreateOrOpen(mapName, Size);
                _view = _map.CreateViewAccessor(0, Size);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Without the block the game simply keeps its original colors.
                _map?.Dispose();
                _map = null;
            }
        }

        public void Publish(int saturation)
        {
            _view?.Write(0, Clamp(saturation));
            _view?.Write(4, Ready);
        }

        public void Dispose()
        {
            _view?.Dispose();
            _map?.Dispose();
        }
    }

    /// <summary>The GameHost side: reads the current value; retries opening the block while the launcher has not created it yet.</summary>
    public sealed class Reader : IDisposable
    {
        private readonly string _mapName;
        private MemoryMappedFile? _map;
        private MemoryMappedViewAccessor? _view;
        private long _nextAttempt;

        public Reader(string mapName = MapName)
        {
            _mapName = mapName;
        }

        /// <summary>Current saturation (0..100). Original colors when the launcher is not reachable.</summary>
        public int Current
        {
            get
            {
                if (_view is null && !TryOpen())
                {
                    return Original;
                }

                return _view!.ReadInt32(4) == Ready ? Clamp(_view.ReadInt32(0)) : Original;
            }
        }

        private bool TryOpen()
        {
            long now = Environment.TickCount64;
            if (now < _nextAttempt || !OperatingSystem.IsWindows())
            {
                return false;
            }

            _nextAttempt = now + 1000;
            try
            {
                _map = MemoryMappedFile.OpenExisting(_mapName, MemoryMappedFileRights.Read);
                _view = _map.CreateViewAccessor(0, Size, MemoryMappedFileAccess.Read);
                return true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FileNotFoundException)
            {
                _map?.Dispose();
                _map = null;
                return false;
            }
        }

        public void Dispose()
        {
            _view?.Dispose();
            _map?.Dispose();
        }
    }
}

/// <summary>Per-pixel saturation for 32-bit BGRA frames (alpha untouched).</summary>
public static class GameColorMath
{
    /// <summary>Moves every pixel toward its luminance gray. <paramref name="saturation"/> 100 = unchanged, 0 = gray.</summary>
    public static void Desaturate(Span<uint> pixels, int saturation)
    {
        int s = GameColorChannel.Clamp(saturation);
        if (s >= GameColorChannel.Original)
        {
            return;
        }

        int keep = s * 256 / GameColorChannel.Original;
        for (int i = 0; i < pixels.Length; i++)
        {
            uint p = pixels[i];
            int b = (int)(p & 0xFF);
            int g = (int)((p >> 8) & 0xFF);
            int r = (int)((p >> 16) & 0xFF);
            int gray = ((r * 77) + (g * 150) + (b * 29)) >> 8;
            r = gray + (((r - gray) * keep) >> 8);
            g = gray + (((g - gray) * keep) >> 8);
            b = gray + (((b - gray) * keep) >> 8);
            pixels[i] = (p & 0xFF000000u) | ((uint)r << 16) | ((uint)g << 8) | (uint)b;
        }
    }

    /// <summary>Same as <see cref="Desaturate(Span{uint},int)"/> over a strided bitmap (rows of <paramref name="width"/> pixels).</summary>
    public static unsafe void Desaturate(nint bits, int strideBytes, int width, int height, int saturation)
    {
        if (bits == 0 || width <= 0 || height <= 0)
        {
            return;
        }

        for (int y = 0; y < height; y++)
        {
            Desaturate(new Span<uint>((void*)(bits + ((nint)y * strideBytes)), width), saturation);
        }
    }
}

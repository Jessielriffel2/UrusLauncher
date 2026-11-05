using System.IO.MemoryMappedFiles;

namespace LegendLauncher.Core.GameDisplay;

/// <summary>
/// The launcher's game zoom. The Flash game was made for about 1000x600 pixels: on a TV or a 4K screen with a high Windows
/// scale it received every physical pixel, so the battle stayed a small 1000x600 box in the middle and the world map did not
/// fill the screen. With zoom the GameHost makes Flash believe its window is smaller (physical size / scale) and enlarges each
/// frame when it is shown, like a browser does. <see cref="Automatic"/> follows the Windows scale of the monitor, never letting
/// the game get smaller than <see cref="MinimumLogicalWidth"/> x <see cref="MinimumLogicalHeight"/>; on a 100% monitor nothing
/// changes.
/// </summary>
public static class GameZoom
{
    /// <summary>Follows the Windows scale of the monitor (the default).</summary>
    public const int Automatic = 0;

    /// <summary>No zoom: one game pixel per screen pixel (the behavior before the zoom existed).</summary>
    public const int Original = 100;

    public const int Maximum = 300;

    /// <summary>The size the game was designed for; the zoom never makes the game area smaller than this.</summary>
    public const int MinimumLogicalWidth = 1000;

    public const int MinimumLogicalHeight = 600;

    /// <summary>Scales this close to 1 only blur the picture without making anything visibly larger.</summary>
    private const double SmallestUsefulScale = 1.05;

    /// <summary>Options offered by the launcher, in display order.</summary>
    public static IReadOnlyList<int> Choices { get; } = [Automatic, 100, 125, 150, 175, 200, 250, 300];

    /// <summary>Any stored or received value becomes a valid choice: 0 (automatic) or a percentage between 100 and 300.</summary>
    public static int Normalize(int choice) => choice == Automatic ? Automatic : Math.Clamp(choice, Original, Maximum);

    /// <summary>
    /// Physical pixels per game pixel (1 = no zoom) for a game area of <paramref name="physicalWidth"/> x
    /// <paramref name="physicalHeight"/> pixels on a monitor whose Windows scale is <paramref name="monitorScale"/> (1.0 = 100%).
    /// </summary>
    public static double ComputeScale(int choice, int physicalWidth, int physicalHeight, double monitorScale)
    {
        if (physicalWidth <= 0 || physicalHeight <= 0)
        {
            return 1;
        }

        int normalized = Normalize(choice);
        double wanted = normalized == Automatic ? monitorScale : normalized / 100d;
        if (!double.IsFinite(wanted) || wanted < SmallestUsefulScale)
        {
            return 1;
        }

        double fit = Math.Min(
            physicalWidth / (double)MinimumLogicalWidth,
            physicalHeight / (double)MinimumLogicalHeight);
        double scale = Math.Min(wanted, fit);
        return scale < SmallestUsefulScale ? 1 : scale;
    }

    public const string MapName = @"Local\UrusLauncher.GameZoom.v1";

    private const int Size = 8;

    // Written next to the value: a block that exists but was never written by the launcher reads as "automatic".
    private const int Ready = 0x315A5255; // "URZ1"

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
                // Without the block every GameHost uses the default (automatic).
                _map?.Dispose();
                _map = null;
            }
        }

        public void Publish(int choice)
        {
            _view?.Write(0, Normalize(choice));
            _view?.Write(4, Ready);
        }

        public void Dispose()
        {
            _view?.Dispose();
            _map?.Dispose();
        }
    }

    /// <summary>The GameHost side: reads the current choice; retries opening the block while the launcher has not created it.</summary>
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

        /// <summary>Current choice (0 = automatic, else 100..300). Automatic when the launcher is not reachable.</summary>
        public int Current
        {
            get
            {
                if (_view is null && !TryOpen())
                {
                    return Automatic;
                }

                return _view!.ReadInt32(4) == Ready ? Normalize(_view.ReadInt32(0)) : Automatic;
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

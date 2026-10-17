using System.IO.MemoryMappedFiles;
using LegendLauncher.App.Services;
using LegendLauncher.Core.GameColor;

namespace LegendLauncher.Tests.App;

public sealed class GameColorSaturationTests
{
    [Fact]
    public void FullSaturationKeepsEveryPixel()
    {
        uint[] pixels = [0xFFFF0000, 0x8000FF00, 0xFF123456];
        uint[] copy = [.. pixels];

        GameColorMath.Desaturate(pixels, 100);

        Assert.Equal(copy, pixels);
    }

    [Fact]
    public void ZeroSaturationTurnsPixelsGrayAndKeepsAlpha()
    {
        uint[] pixels = [0xFFFF0000, 0x8000FF00, 0x400000FF];

        GameColorMath.Desaturate(pixels, 0);

        foreach (uint p in pixels)
        {
            uint r = (p >> 16) & 0xFF, g = (p >> 8) & 0xFF, b = p & 0xFF;
            Assert.Equal(r, g);
            Assert.Equal(g, b);
        }

        Assert.Equal(0xFFu, pixels[0] >> 24);
        Assert.Equal(0x80u, pixels[1] >> 24);
        Assert.Equal(0x40u, pixels[2] >> 24);
    }

    [Fact]
    public void HalfSaturationMovesRedHalfwayToItsGray()
    {
        uint[] pixels = [0xFFFF0000];

        GameColorMath.Desaturate(pixels, 50);

        int gray = (255 * 77) >> 8;
        int r = (int)((pixels[0] >> 16) & 0xFF);
        int g = (int)((pixels[0] >> 8) & 0xFF);
        Assert.InRange(r, gray + ((255 - gray) / 2) - 2, gray + ((255 - gray) / 2) + 2);
        Assert.InRange(g, (gray / 2) - 2, (gray / 2) + 2);
    }

    [Fact]
    public void GameHostReadsWhatTheLauncherPublishes()
    {
        string name = @"Local\UrusLauncher.GameColor.test." + Guid.NewGuid().ToString("N");
        using var reader = new GameColorChannel.Reader(name);
        Assert.Equal(100, reader.Current);

        using var writer = new GameColorChannel.Writer(name);
        writer.Publish(35);
        using var lateReader = new GameColorChannel.Reader(name);
        Assert.Equal(35, lateReader.Current);

        writer.Publish(250);
        Assert.Equal(100, lateReader.Current);
    }

    [Fact]
    public void AnUnwrittenBlockNeverTurnsTheGameGray()
    {
        string name = @"Local\UrusLauncher.GameColor.test." + Guid.NewGuid().ToString("N");
        using MemoryMappedFile created = MemoryMappedFile.CreateOrOpen(name, 8);

        using var reader = new GameColorChannel.Reader(name);

        Assert.Equal(100, reader.Current);
    }

    [Fact]
    public async Task SettingsFromBeforeTheSliderKeepOriginalColorsAndTheValueIsSaved()
    {
        string directory = Path.Combine(Path.GetTempPath(), "urus-color-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, "settings.json");
        try
        {
            await File.WriteAllTextAsync(file, "{\"IsGameMuted\":false,\"LayoutMode\":1,\"LanguageCode\":\"pt-BR\"}");
            var service = new LauncherSettingsService(file);

            Assert.Equal(100, (await service.LoadAsync()).ColorSaturation);

            await service.SaveGamePreferencesAsync(false, GameLayoutMode.Single, 40);
            Assert.Equal(40, (await service.LoadAsync()).ColorSaturation);

            await service.SaveGamePreferencesAsync(true, GameLayoutMode.Single);
            Assert.Equal(40, (await service.LoadAsync()).ColorSaturation);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FilterIsInstalledWithTheFlashControlAndOnlyTouchesWindowFrames()
    {
        string filter = File.ReadAllText(FindRepositoryFile("src", "LegendLauncher.GameHost.Legacy", "FlashColorFilter.cs"));
        string flash = File.ReadAllText(FindRepositoryFile("src", "LegendLauncher.GameHost.Legacy", "FlashActiveXControl.cs"));

        Assert.Contains("FlashColorFilter.Install(flashOcxPath)", flash, StringComparison.Ordinal);
        foreach (string import in new[] { "\"BitBlt\"", "\"StretchBlt\"", "\"StretchDIBits\"" })
        {
            Assert.Contains(import, filter, StringComparison.Ordinal);
        }

        // frames shown on a window only: Flash's internal copies and the macro's PrintWindow captures keep the real colors
        Assert.Contains("WindowFromDC(hdcDest) == nint.Zero", filter, StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(params string[] relativeSegments)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LegendLauncherNext.slnx")))
            {
                return Path.Combine([directory.FullName, .. relativeSegments]);
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("LegendLauncherNext repository root was not found.");
    }
}

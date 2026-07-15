using LegendLauncher.Infrastructure.Paths;

namespace LegendLauncher.Tests.Infrastructure;

public sealed class AppPathsTests
{
    [Fact]
    public void Constructor_BuildsOwnedPathsBelowLocalApplicationData()
    {
        using var temporaryDirectory = new TemporaryDirectory();

        var paths = new AppPaths(temporaryDirectory.Path);

        var expectedRoot = temporaryDirectory.Combine(AppPaths.DefaultApplicationDirectoryName);
        Assert.Equal(expectedRoot, paths.RootDirectory);
        Assert.Equal(Path.Combine(expectedRoot, "cache"), paths.CacheDirectory);
        Assert.Equal(Path.Combine(expectedRoot, "data"), paths.DataDirectory);
        Assert.Equal(Path.Combine(expectedRoot, "updates"), paths.UpdatesDirectory);
        Assert.Equal(Path.Combine(expectedRoot, "cache", "server-catalogs.json"), paths.CatalogCacheFile);
        Assert.Equal(Path.Combine(expectedRoot, "data", "profiles.json"), paths.ProfilesFile);
        Assert.Equal(Path.Combine(expectedRoot, "data", "settings.json"), paths.SettingsFile);
        Assert.False(Directory.Exists(expectedRoot));
    }

    [Fact]
    public void EnsureDirectories_CreatesAllWritableDirectories()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = new AppPaths(temporaryDirectory.Path);

        paths.EnsureDirectories();

        Assert.True(Directory.Exists(paths.CacheDirectory));
        Assert.True(Directory.Exists(paths.DataDirectory));
        Assert.True(Directory.Exists(paths.UpdatesDirectory));
    }
}

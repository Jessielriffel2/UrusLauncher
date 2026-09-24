using System.Windows.Media;
using System.Windows.Media.Imaging;
using LegendLauncher.App.Services;
using LegendLauncher.Tests.Infrastructure;

namespace LegendLauncher.Tests.App;

public sealed class ProfileAvatarStoreTests
{
    [Fact]
    public void SupportedExtensions_AreRestrictedToRequestedFormats()
    {
        Assert.True(ProfileAvatarStore.IsSupportedExtension("profile.png"));
        Assert.True(ProfileAvatarStore.IsSupportedExtension("profile.JPEG"));
        Assert.True(ProfileAvatarStore.IsSupportedExtension("profile.webp"));
        Assert.False(ProfileAvatarStore.IsSupportedExtension("profile.gif"));
        Assert.False(ProfileAvatarStore.IsSupportedExtension("profile"));
    }

    [Fact]
    public async Task ImportAsync_CopiesAndResizesImageToOwnedDirectory()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        string avatarDirectory = temporaryDirectory.Combine("avatars");
        string sourcePath = temporaryDirectory.Combine("source.png");
        var source = new WriteableBitmap(641, 421, 96, 96, PixelFormats.Bgra32, null);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        await using (var stream = new FileStream(sourcePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            encoder.Save(stream);
        }

        var store = new ProfileAvatarStore(avatarDirectory);
        string fileName = await store.ImportAsync(sourcePath);
        string copiedPath = Path.Combine(avatarDirectory, fileName);

        Assert.EndsWith(".png", fileName, StringComparison.Ordinal);
        Assert.True(File.Exists(copiedPath));
        Assert.NotEqual(sourcePath, copiedPath);
        ImageSource? loaded = store.Load(fileName);
        BitmapSource loadedBitmap = Assert.IsAssignableFrom<BitmapSource>(loaded);
        Assert.InRange(loadedBitmap.PixelWidth, 1, ProfileAvatarStore.MaximumDimension);
        Assert.InRange(loadedBitmap.PixelHeight, 1, ProfileAvatarStore.MaximumDimension);

        await store.DeleteAsync(fileName);
        Assert.False(File.Exists(copiedPath));
        Assert.Null(store.Load(fileName));
    }

    [Fact]
    public async Task Load_RejectsPathsOutsideOwnedDirectory()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var store = new ProfileAvatarStore(temporaryDirectory.Combine("avatars"));
        string outsidePath = temporaryDirectory.Combine("outside.png");
        await File.WriteAllTextAsync(outsidePath, "not an image");

        Assert.Null(store.Load(Path.Combine("..", "outside.png")));
    }
}

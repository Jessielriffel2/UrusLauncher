using System.Buffers.Binary;
using System.Xml.Linq;

namespace LegendLauncher.Tests.App;

public sealed class BrandingAssetTests
{
    private static readonly byte[] PngSignature =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public void UrusLogo_IsSquareRgbaPngAndWindowsIconIsConfigured()
    {
        string root = FindRepositoryRoot();
        string branding = Path.Combine(
            root,
            "src",
            "LegendLauncher.App",
            "Assets",
            "Branding");
        byte[] logo = File.ReadAllBytes(Path.Combine(branding, "urus-logo.png"));
        string icon = Path.Combine(branding, "urus-launcher.ico");

        Assert.True(logo.AsSpan(0, 8).SequenceEqual(PngSignature));
        Assert.Equal(1024, BinaryPrimitives.ReadInt32BigEndian(logo.AsSpan(16, 4)));
        Assert.Equal(1024, BinaryPrimitives.ReadInt32BigEndian(logo.AsSpan(20, 4)));
        Assert.Equal(8, logo[24]);
        Assert.Equal(6, logo[25]);
        Assert.True(new FileInfo(icon).Length > 10_000);

        XDocument project = XDocument.Load(Path.Combine(
            root,
            "src",
            "LegendLauncher.App",
            "LegendLauncher.App.csproj"));
        Assert.Contains(
            project.Descendants("Resource"),
            element => element.Attribute("Include")?.Value ==
                "Assets\\Branding\\*.png");
        Assert.Equal(
            "Assets\\Branding\\urus-launcher.ico",
            project.Descendants("ApplicationIcon").Single().Value);
    }

    [Fact]
    public void UrusLogo_ReplacesTheLegacyMarksOnEveryBrandedWindow()
    {
        string root = FindRepositoryRoot();
        string app = Path.Combine(root, "src", "LegendLauncher.App");
        string mainWindow = File.ReadAllText(Path.Combine(app, "MainWindow.xaml"));
        string detachedWindow = File.ReadAllText(Path.Combine(
            app,
            "Views",
            "Game",
            "DetachedGameWindow.xaml"));

        Assert.Contains("Assets/Branding/urus-logo.png", mainWindow);
        Assert.Contains(
            "/UrusLauncher.App;component/Assets/Branding/urus-logo.png",
            detachedWindow);
        Assert.False(File.Exists(Path.Combine(
            app,
            "Assets",
            "legend-launcher-mark.png")));
        Assert.False(File.Exists(Path.Combine(
            app,
            "Assets",
            "legend-launcher-mark-refined.png")));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LegendLauncherNext.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "LegendLauncherNext repository root was not found.");
    }
}

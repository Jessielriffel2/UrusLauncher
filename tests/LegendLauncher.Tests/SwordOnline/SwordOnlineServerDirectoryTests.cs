using LegendLauncher.Core.Models;
using LegendLauncher.Providers.SwordOnline;

namespace LegendLauncher.Tests.SwordOnline;

public sealed class SwordOnlineServerDirectoryTests
{
    [Fact]
    public void Catalog_DeclaresASinglePasswordPlatformOverHttps()
    {
        PlatformDefinition platform = SwordOnlinePlatformCatalog.SwordOnline;

        Assert.Equal("swordonline", platform.Id);
        Assert.True(platform.RequiresPassword);
        Assert.Equal("https://swordonline.tech/config.xml", platform.ServerListEndpoint.AbsoluteUri);
        Assert.True(platform.IsHot);
        Assert.Single(SwordOnlinePlatformCatalog.All);
        Assert.Same(platform, SwordOnlinePlatformCatalog.Find("SWORDONLINE"));
        Assert.Null(SwordOnlinePlatformCatalog.Find("elarionis-lo"));
    }

    [Fact]
    public async Task GetServersAsync_ReturnsTheOnlyServerAlwaysLaunchable()
    {
        ServerCatalog catalog = await new SwordOnlineServerDirectory()
            .GetServersAsync(SwordOnlinePlatformCatalog.SwordOnline);

        GameServer server = Assert.Single(catalog.All);
        Assert.True(server.IsValid);
        Assert.True(server.IsRecommended);
        Assert.Equal("https://swordonline.tech/Loading.swf", server.LaunchUri!.AbsoluteUri);
        Assert.Same(server, catalog.Current);
    }

    [Fact]
    public async Task GetServersAsync_NeverNeedsTheNetwork_SoASlowOrOfflinePcStillGetsAPlayableServer()
    {
        // No HttpClient is even constructed: the answer is immediate and cannot time out.
        Task<ServerCatalog> pending = new SwordOnlineServerDirectory()
            .GetServersAsync(SwordOnlinePlatformCatalog.SwordOnline);

        Assert.True(pending.IsCompletedSuccessfully);
        Assert.True(Assert.Single((await pending).All).IsValid);
    }

    [Fact]
    public async Task GetServersAsync_HonoursCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new SwordOnlineServerDirectory()
            .GetServersAsync(SwordOnlinePlatformCatalog.SwordOnline, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task GetServersAsync_ForAnotherPlatformReturnsNothing()
    {
        PlatformDefinition other = SwordOnlinePlatformCatalog.SwordOnline with { Id = "other" };

        ServerCatalog catalog = await new SwordOnlineServerDirectory().GetServersAsync(other);

        Assert.Empty(catalog.All);
    }
}

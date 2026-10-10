using System.Net;
using LegendLauncher.Core.Models;
using LegendLauncher.Providers.SwordOnline;
using LegendLauncher.Tests.Oas;

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
    public async Task GetServersAsync_ReturnsTheOnlyServerAsValidWhenConfigIsServed()
    {
        Uri? probed = null;
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            probed = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        ServerCatalog catalog = await new SwordOnlineServerDirectory(new HttpClient(handler))
            .GetServersAsync(SwordOnlinePlatformCatalog.SwordOnline);

        GameServer server = Assert.Single(catalog.All);
        Assert.True(server.IsValid);
        Assert.True(server.IsRecommended);
        Assert.Equal("https://swordonline.tech/Loading.swf", server.LaunchUri!.AbsoluteUri);
        Assert.Equal("https://swordonline.tech/config.xml", probed!.AbsoluteUri);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task GetServersAsync_MarksTheServerInvalidWhenThePortalFails(HttpStatusCode status)
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(status)));

        ServerCatalog catalog = await new SwordOnlineServerDirectory(new HttpClient(handler))
            .GetServersAsync(SwordOnlinePlatformCatalog.SwordOnline);

        Assert.False(Assert.Single(catalog.All).IsValid);
    }

    [Fact]
    public async Task GetServersAsync_MarksTheServerInvalidWhenThePortalIsUnreachable()
    {
        var handler = new StubHttpMessageHandler((_, _) => throw new HttpRequestException("offline"));

        ServerCatalog catalog = await new SwordOnlineServerDirectory(new HttpClient(handler))
            .GetServersAsync(SwordOnlinePlatformCatalog.SwordOnline);

        Assert.False(Assert.Single(catalog.All).IsValid);
    }

    [Fact]
    public async Task GetServersAsync_ForAnotherPlatformReturnsNothingWithoutTouchingTheNetwork()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        PlatformDefinition other = SwordOnlinePlatformCatalog.SwordOnline with { Id = "other" };

        ServerCatalog catalog = await new SwordOnlineServerDirectory(new HttpClient(handler)).GetServersAsync(other);

        Assert.Empty(catalog.All);
        Assert.Equal(0, handler.CallCount);
    }
}

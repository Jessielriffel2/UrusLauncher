using System.Net;
using System.Text;
using LegendLauncher.Core.Models;
using LegendLauncher.Providers.Elarionis;
using LegendLauncher.Tests.Oas;

namespace LegendLauncher.Tests.Elarionis;

public sealed class ElarionisServerDirectoryTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Catalog_ExposesASinglePlatformWithFifteenServers()
    {
        Assert.Single(ElarionisPlatformCatalog.All);
        Assert.Equal("elarionis-lo", ElarionisPlatformCatalog.Elarionis.Id);
        Assert.Equal("Elarionis Online", ElarionisPlatformCatalog.Elarionis.DisplayName);
        Assert.Equal(
            new Uri("https://elarionis.online/api/shards"),
            ElarionisPlatformCatalog.Elarionis.ServerListEndpoint);
        Assert.NotNull(ElarionisPlatformCatalog.Find("ELARIONIS-LO"));
    }

    [Fact]
    public async Task GetServersAsync_ParsesShardsInNumericOrder()
    {
        const string json = """
            {
              "ok": true,
              "shards": [
                {"number": 7, "site": "s7", "name": "Kapadokya", "label": "S7 - KAPADOKYA", "path": "/s7/", "state": "calm", "waiting": 0, "char_full": false},
                {"number": 1, "site": "local", "name": "Racon", "label": "S1 - RACON", "path": "/", "state": "normal", "waiting": 3, "char_full": false},
                {"number": 15, "site": "s15", "name": "Asgard", "label": "S15 - ASGARD", "path": "/s15/", "state": "offline", "waiting": 0, "char_full": false}
              ]
            }
            """;
        Uri? requestUri = null;
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            requestUri = request.RequestUri;
            return Task.FromResult(JsonResponse(json));
        });
        var cache = new TestCatalogCache();
        var directory = CreateDirectory(handler, cache);

        ServerCatalog catalog = await directory.GetServersAsync(ElarionisPlatformCatalog.Elarionis);

        Assert.Equal(ElarionisPlatformCatalog.Elarionis.ServerListEndpoint, requestUri);
        Assert.Equal("elarionis-lo", catalog.PlatformId);
        Assert.Equal(3, catalog.All.Count);
        Assert.Equal(["1", "7", "15"], catalog.All.Select(static server => server.Id));
        Assert.Equal("S1", catalog.All[0].Code);
        Assert.Equal("S1 - RACON", catalog.All[0].FullName);
        Assert.Equal(new Uri("https://elarionis.online/play?site=local"), catalog.All[0].LaunchUri);
        Assert.Equal(new Uri("https://elarionis.online/s7/play?site=s7"), catalog.All[1].LaunchUri);
        Assert.True(catalog.All[0].IsValid);
        Assert.True(catalog.All[1].IsValid);
        Assert.False(catalog.All[2].IsValid);
        Assert.Empty(catalog.Played);
        Assert.Null(catalog.Current);
        Assert.Equal(1, cache.SetCount);
    }

    [Fact]
    public async Task GetServersAsync_RejectsTamperedPlatformBeforeNetwork()
    {
        PlatformDefinition platform = ElarionisPlatformCatalog.Elarionis with
        {
            ServerListEndpoint = new Uri("https://attacker.example/shards"),
        };
        var handler = new StubHttpMessageHandler((_, _) =>
            throw new InvalidOperationException("Network must not be used."));
        var directory = CreateDirectory(handler);

        await Assert.ThrowsAsync<ArgumentException>(() => directory.GetServersAsync(platform));

        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task GetServersAsync_RejectsChangedEffectiveAddress()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = new HttpRequestMessage(
                    HttpMethod.Get,
                    "https://attacker.example/shards"),
                Content = new StringContent(
                    "{\"ok\":true,\"shards\":[]}",
                    Encoding.UTF8,
                    "application/json"),
            }));
        var directory = CreateDirectory(handler);

        ElarionisServerDirectoryException exception =
            await Assert.ThrowsAsync<ElarionisServerDirectoryException>(() =>
                directory.GetServersAsync(ElarionisPlatformCatalog.Elarionis));

        Assert.IsType<ElarionisServerDirectoryException>(exception.InnerException);
    }

    [Fact]
    public async Task GetServersAsync_FallsBackToPlatformIsolatedCache()
    {
        var cachedServer = new GameServer("7", "Cached");
        var cache = new TestCatalogCache
        {
            Value = new ServerCatalog(
                ElarionisPlatformCatalog.Elarionis.Id,
                [cachedServer],
                [],
                null,
                FixedNow),
        };
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var directory = CreateDirectory(handler, cache);

        ServerCatalog catalog = await directory.GetServersAsync(ElarionisPlatformCatalog.Elarionis);

        Assert.True(catalog.IsFromCache);
        Assert.Equal("7", catalog.All.Single().Id);
        Assert.Equal(1, cache.GetCount);
    }

    [Theory]
    [InlineData("https://elarionis.online/s7/play?site=s7", true)]
    [InlineData("https://elarionis.online/play?site=local", true)]
    [InlineData("https://elarionis.online/s7/play", false)]
    [InlineData("https://elarionis.online/servers?token=abc", false)]
    [InlineData("https://attacker.example/s7/play?site=s7", false)]
    [InlineData("http://elarionis.online/s7/play?site=s7", false)]
    public void OriginPolicy_ValidatesPlayDocumentUris(string address, bool expected)
    {
        Assert.Equal(expected, ElarionisOriginPolicy_IsPlayDocument(address));
    }

    private static bool ElarionisOriginPolicy_IsPlayDocument(string address) =>
        ElarionisOriginPolicy.IsPlayDocumentUri(new Uri(address));

    private static ElarionisServerDirectory CreateDirectory(
        StubHttpMessageHandler handler,
        TestCatalogCache? cache = null) =>
        new(
            new HttpClient(handler),
            cache,
            TimeSpan.FromSeconds(1),
            new FixedTimeProvider(FixedNow));

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

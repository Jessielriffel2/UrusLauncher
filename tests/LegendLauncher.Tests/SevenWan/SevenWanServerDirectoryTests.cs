using System.Net;
using System.Text;
using LegendLauncher.Core.Models;
using LegendLauncher.Providers.SevenWan;
using LegendLauncher.Tests.Oas;

namespace LegendLauncher.Tests.SevenWan;

public sealed class SevenWanServerDirectoryTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Catalog_ContainsTheFourteenLegacySelectorOptionsInOrder()
    {
        string[] expected =
        [
            "Wartune - R2-US West (7wan)",
            "Wartune - R2-US East (7wan)",
            "Wartune - R2-Oceanic (7wan)",
            "Wartune - R2-Europe (7wan)",
            "Wartune - Proficient Ct (7wan)",
            "Wartune - Koramgame (7wan)",
            "Wartune - Kongregate (7wan)",
            "Wartune - Kabam (7wan)",
            "Wartune - Ennia (7wan)",
            "Wartune - Armor (7wan)",
            "Wartune - Agame (7wan)",
            "Wartune Reborn - R2 (7wan)",
            "Wartune Reborn - Proficient-Ct (7wan)",
            "Wartune Reborn - Kongregate (7wan)",
        ];

        Assert.Equal(expected, SevenWanPlatformCatalog.All.Select(static item => item.DisplayName));
        Assert.Equal(14, SevenWanPlatformCatalog.All.Select(static item => item.Id).Distinct().Count());
        Assert.All(SevenWanPlatformCatalog.All, static item =>
            Assert.Equal(Uri.UriSchemeHttps, item.ServerListEndpoint.Scheme));
    }

    [Fact]
    public async Task GetServersAsync_UsesSelectedBucketAndConservativeLifecycleState()
    {
        const string json = """
            {
              "plat": { "10": "R2", "13": "Proficient-Ct" },
              "server": {
                "10": [
                  {
                    "sid": 900,
                    "servername": "Wrong bucket",
                    "line": 90,
                    "status": "1",
                    "stop_service": 0,
                    "server_delete": 0
                  }
                ],
                "13": [
                  {
                    "sid": 2559,
                    "servername": "[Wartune-Reborn S131]",
                    "line": 131,
                    "status": "1",
                    "stop_service": 1,
                    "server_delete": 1,
                    "start_time": 1781784000
                  },
                  {
                    "sid": 2563,
                    "servername": "[Wartune-Reborn S132]",
                    "line": 132,
                    "status": 1,
                    "stop_service": 0,
                    "server_delete": 0
                  }
                ]
              }
            }
            """;
        PlatformDefinition platform = SevenWanPlatformCatalog.All.Single(
            static item => item.DisplayName == "Wartune Reborn - Proficient-Ct (7wan)");
        Uri? requestUri = null;
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            requestUri = request.RequestUri;
            return Task.FromResult(JsonResponse(json));
        });
        var cache = new TestCatalogCache();
        var directory = CreateDirectory(handler, cache);

        ServerCatalog catalog = await directory.GetServersAsync(platform);

        Assert.Equal(platform.ServerListEndpoint, requestUri);
        Assert.Equal(platform.Id, catalog.PlatformId);
        Assert.Equal(2, catalog.All.Count);
        Assert.DoesNotContain(catalog.All, static server => server.Name == "Wrong bucket");
        GameServer stopped = catalog.All[0];
        Assert.Equal("S131", stopped.Code);
        Assert.Equal(new Uri("https://7.wan.com/game/login/?sid=2559"), stopped.LaunchUri);
        Assert.False(stopped.IsValid);
        Assert.False(stopped.IsRecommended);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1781784000), stopped.StartTimeUtc);
        Assert.True(catalog.All[1].IsValid);
        Assert.Equal(1, cache.SetCount);
    }

    [Fact]
    public async Task GetServersAsync_RejectsTamperedPlatformBeforeNetwork()
    {
        PlatformDefinition platform = SevenWanPlatformCatalog.All[0] with
        {
            ServerListEndpoint = new Uri("https://attacker.example/catalog"),
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
        PlatformDefinition platform = SevenWanPlatformCatalog.All[0];
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = new HttpRequestMessage(
                    HttpMethod.Get,
                    "https://attacker.example/catalog"),
                Content = new StringContent(
                    "{\"server\":{\"6\":[{\"sid\":1,\"servername\":\"S1\"}]}}",
                    Encoding.UTF8,
                    "application/json"),
            }));
        var directory = CreateDirectory(handler);

        SevenWanServerDirectoryException exception =
            await Assert.ThrowsAsync<SevenWanServerDirectoryException>(() =>
                directory.GetServersAsync(platform));

        Assert.IsType<SevenWanServerDirectoryException>(exception.InnerException);
    }

    [Fact]
    public async Task GetServersAsync_FallsBackToPlatformIsolatedCache()
    {
        PlatformDefinition platform = SevenWanPlatformCatalog.All[0];
        var cachedServer = new GameServer("77", "Cached");
        var cache = new TestCatalogCache
        {
            Value = new ServerCatalog(
                platform.Id,
                [cachedServer],
                [],
                null,
                FixedNow),
        };
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var directory = CreateDirectory(handler, cache);

        ServerCatalog catalog = await directory.GetServersAsync(platform);

        Assert.True(catalog.IsFromCache);
        Assert.Equal("77", catalog.All.Single().Id);
        Assert.Equal(1, cache.GetCount);
    }

    private static SevenWanServerDirectory CreateDirectory(
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

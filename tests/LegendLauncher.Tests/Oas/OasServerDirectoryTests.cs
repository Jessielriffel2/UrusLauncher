using System.Net;
using System.Text;
using System.Text.Json;
using LegendLauncher.Core.Contracts;
using LegendLauncher.Core.Models;
using LegendLauncher.Providers.Oas;

namespace LegendLauncher.Tests.Oas;

public sealed class OasServerDirectoryTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetServersAsync_ParsesNormalizesAndCachesOfficialFields()
    {
        const string json = """
            {
              "data": {
                "servers": {
                  "all": [
                    {
                      "server_sid": " 001 ",
                      "server_id": 1,
                      "server_prex": " OAS ",
                      "name": " Lua ",
                      "fullname": " OAS1:   Sombra sob a Lua ",
                      "url": "//s1.example/game",
                      "recommand": "1",
                      "valid": true,
                      "merger": " grupo-1 ",
                      "start_time": 1704067200
                    },
                    {
                      "server_sid": "2",
                      "fullname": " Servidor   Dois ",
                      "is_valid": "0"
                    },
                    {
                      "server_sid": "001",
                      "fullname": "duplicado"
                    }
                  ],
                  "played": [
                    { "id": "001", "name": "nome antigo" },
                    { "id": 2, "name": "Dois" }
                  ],
                  "current": { "id": "001", "name": "Atual" }
                }
              }
            }
            """;
        Uri? capturedUri = null;
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            capturedUri = request.RequestUri;
            return Task.FromResult(JsonResponse(json));
        });
        var cache = new TestCatalogCache();
        var directory = CreateDirectory(handler, cache);

        var catalog = await directory.GetServersAsync(OasPlatformCatalog.Brazil);

        Assert.NotNull(capturedUri);
        Assert.Equal(Uri.UriSchemeHttps, capturedUri.Scheme);
        Assert.Contains("gamecode=lobr", capturedUri.Query, StringComparison.Ordinal);
        Assert.Contains("uid=0", capturedUri.Query, StringComparison.Ordinal);
        Assert.Equal(ServerCatalogSource.Remote, catalog.Source);
        Assert.Equal(FixedNow, catalog.RetrievedAtUtc);
        Assert.Equal(2, catalog.All.Count);
        Assert.Equal(2, catalog.Played.Count);
        Assert.Equal("001", catalog.Current?.Id);

        var first = catalog.All[0];
        Assert.Equal(1, first.NumericId);
        Assert.Equal("OAS", first.Code);
        Assert.Equal("Lua", first.Name);
        Assert.Equal("OAS1: Sombra sob a Lua", first.FullName);
        Assert.Equal(new Uri("https://s1.example/game"), first.LaunchUri);
        Assert.True(first.IsRecommended);
        Assert.True(first.IsValid);
        Assert.Equal("grupo-1", first.Merger);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1704067200), first.StartTimeUtc);
        Assert.True(first.IsAvailable(FixedNow));
        Assert.False(catalog.All[1].IsAvailable(FixedNow));
        Assert.NotNull(cache.Value);
        Assert.Equal(catalog.All, cache.Value.All);
        Assert.Empty(cache.Value.Played);
        Assert.Null(cache.Value.Current);
        Assert.Equal(1, cache.SetCount);
    }

    [Fact]
    public async Task GetServersAsync_RemovesAccountHistoryFromLegacyCacheFallback()
    {
        var server = new GameServer("77", "Cache");
        var cache = new TestCatalogCache
        {
            Value = new ServerCatalog(
                OasPlatformCatalog.Brazil.Id,
                [server],
                [server],
                server,
                FixedNow),
        };
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var directory = CreateDirectory(handler, cache);

        ServerCatalog result = await directory.GetServersAsync(OasPlatformCatalog.Brazil, userId: 999);

        Assert.True(result.IsFromCache);
        Assert.Empty(result.Played);
        Assert.Null(result.Current);
    }

    [Fact]
    public async Task GetServersAsync_ParsesDictionaryCollectionsAndCustomUserId()
    {
        const string json = """
            {
              "all": {
                "10": " Décimo ",
                "11": { "id": "11", "name": " Onze " }
              },
              "played": { "10": "Décimo" },
              "current": "11"
            }
            """;
        Uri? capturedUri = null;
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            capturedUri = request.RequestUri;
            return Task.FromResult(JsonResponse(json));
        });
        var directory = CreateDirectory(handler);

        var catalog = await directory.GetServersAsync(OasPlatformCatalog.Brazil, 42);

        Assert.NotNull(capturedUri);
        Assert.Contains("gamecode=lobr", capturedUri.Query, StringComparison.Ordinal);
        Assert.Contains("uid=42", capturedUri.Query, StringComparison.Ordinal);
        Assert.Equal(["10", "11"], catalog.All.Select(server => server.Id));
        Assert.Equal("Décimo", catalog.Played.Single().Name);
        Assert.Equal("11", catalog.Current?.Id);
        Assert.Equal("Onze", catalog.Current?.Name);
    }

    [Fact]
    public async Task GetServersAsync_UsesCacheWhenHttpFails()
    {
        var cached = CatalogWith(new GameServer("77", "Cache"));
        var cache = new TestCatalogCache { Value = cached };
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var directory = CreateDirectory(handler, cache);

        var result = await directory.GetServersAsync(OasPlatformCatalog.Brazil);

        Assert.True(result.IsFromCache);
        Assert.Equal("77", result.All.Single().Id);
        Assert.Equal(1, cache.GetCount);
    }

    [Fact]
    public async Task GetServersAsync_UsesCacheWhenPayloadIsMalformed()
    {
        var cache = new TestCatalogCache
        {
            Value = CatalogWith(new GameServer("88", "Último catálogo")),
        };
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse("{ conteúdo inválido")));
        var directory = CreateDirectory(handler, cache);

        var result = await directory.GetServersAsync(OasPlatformCatalog.Brazil);

        Assert.True(result.IsFromCache);
        Assert.Equal("88", result.All.Single().Id);
    }

    [Fact]
    public async Task GetServersAsync_ReturnsRemoteCatalogWhenCacheWriteFails()
    {
        var cache = new TestCatalogCache
        {
            SetException = new JsonException("Cache corrompido"),
        };
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse("""{"all":[{"id":"9","name":"Remoto"}]}""")));
        var directory = CreateDirectory(handler, cache);

        var result = await directory.GetServersAsync(OasPlatformCatalog.Brazil);

        Assert.False(result.IsFromCache);
        Assert.Equal("9", result.All.Single().Id);
    }

    [Fact]
    public async Task GetServersAsync_PreservesDirectoryErrorWhenCacheReadFails()
    {
        var cache = new TestCatalogCache
        {
            GetException = new JsonException("Cache corrompido"),
        };
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var directory = CreateDirectory(handler, cache);

        var exception = await Assert.ThrowsAsync<OasServerDirectoryException>(() =>
            directory.GetServersAsync(OasPlatformCatalog.Brazil));

        Assert.IsType<HttpRequestException>(exception.InnerException);
    }

    [Fact]
    public async Task GetServersAsync_UsesCacheAfterInternalTimeout()
    {
        var cache = new TestCatalogCache
        {
            Value = CatalogWith(new GameServer("99", "Offline")),
        };
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable");
        });
        var directory = CreateDirectory(handler, cache, TimeSpan.FromMilliseconds(30));

        var result = await directory.GetServersAsync(OasPlatformCatalog.Brazil);

        Assert.True(result.IsFromCache);
        Assert.Equal("99", result.All.Single().Id);
    }

    [Fact]
    public async Task GetServersAsync_PropagatesCallerCancellationWithoutReadingCache()
    {
        var cache = new TestCatalogCache
        {
            Value = CatalogWith(new GameServer("100", "Não deve ser usado")),
        };
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable");
        });
        var directory = CreateDirectory(handler, cache, TimeSpan.FromSeconds(5));
        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            directory.GetServersAsync(
                OasPlatformCatalog.Brazil,
                cancellationToken: cancellationSource.Token));

        Assert.Equal(0, cache.GetCount);
    }

    [Fact]
    public async Task GetServersAsync_RejectsNonHttpsEndpoint()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            throw new InvalidOperationException("HTTP handler must not be called."));
        var directory = CreateDirectory(handler);
        var platform = OasPlatformCatalog.Brazil with
        {
            ServerListEndpoint = new Uri("http://directory.example/servers"),
        };

        await Assert.ThrowsAsync<ArgumentException>(() => directory.GetServersAsync(platform));

        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task GetServersAsync_RejectsUnverifiedHttpsEndpoint()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            throw new InvalidOperationException("HTTP handler must not be called."));
        var directory = CreateDirectory(handler);
        var platform = OasPlatformCatalog.Brazil with
        {
            ServerListEndpoint = new Uri("https://attacker.example/servers"),
        };

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
                    "https://odp3.oasgames.com/api/game/serverlist?gamecode=lortr&uid=0"),
                Content = new StringContent("{\"all\":[{\"id\":\"1\"}]}"),
            }));
        var directory = CreateDirectory(handler);

        OasServerDirectoryException error = await Assert.ThrowsAsync<OasServerDirectoryException>(() =>
            directory.GetServersAsync(OasPlatformCatalog.Brazil));

        Assert.IsType<OasServerDirectoryException>(error.InnerException);
    }

    [Fact]
    public async Task GetServersAsync_RejectsResponseAboveConfiguredLimit()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse("{\"all\":[{\"id\":\"123456789\"}]}")));
        var directory = new OasServerDirectory(
            new HttpClient(handler),
            requestTimeout: TimeSpan.FromSeconds(1),
            timeProvider: new FixedTimeProvider(FixedNow),
            maxResponseBytes: 8);

        OasServerDirectoryException error = await Assert.ThrowsAsync<OasServerDirectoryException>(() =>
            directory.GetServersAsync(OasPlatformCatalog.Brazil));

        Assert.IsType<OasServerDirectoryException>(error.InnerException);
    }

    [Fact]
    public async Task GetServersAsync_RejectsNegativeUserIdBeforeNetwork()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            throw new InvalidOperationException("HTTP handler must not be called."));
        var directory = CreateDirectory(handler);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            directory.GetServersAsync(OasPlatformCatalog.Brazil, userId: -1));

        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task GetServersAsync_ThrowsProviderExceptionWhenNoCacheExists()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)));
        var directory = CreateDirectory(handler);

        var exception = await Assert.ThrowsAsync<OasServerDirectoryException>(() =>
            directory.GetServersAsync(OasPlatformCatalog.Brazil));

        Assert.IsType<HttpRequestException>(exception.InnerException);
    }

    private static OasServerDirectory CreateDirectory(
        StubHttpMessageHandler handler,
        IServerCatalogCache? cache = null,
        TimeSpan? timeout = null) =>
        new(
            new HttpClient(handler),
            cache,
            timeout,
            new FixedTimeProvider(FixedNow));

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private static ServerCatalog CatalogWith(GameServer server) => new(
        OasPlatformCatalog.Brazil.Id,
        [server],
        [],
        null,
        FixedNow);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

internal sealed class StubHttpMessageHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync)
    : HttpMessageHandler
{
    public int CallCount { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        CallCount++;
        HttpResponseMessage response = await sendAsync(request, cancellationToken);
        response.RequestMessage ??= request;
        return response;
    }
}

internal sealed class TestCatalogCache : IServerCatalogCache
{
    public ServerCatalog? Value { get; set; }

    public Exception? GetException { get; set; }

    public Exception? SetException { get; set; }

    public int GetCount { get; private set; }

    public int SetCount { get; private set; }

    public Task<ServerCatalog?> GetAsync(
        string platformId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GetCount++;
        if (GetException is not null)
        {
            throw GetException;
        }

        return Task.FromResult(Value?.PlatformId == platformId ? Value : null);
    }

    public Task SetAsync(
        ServerCatalog catalog,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SetCount++;
        if (SetException is not null)
        {
            throw SetException;
        }

        Value = catalog;
        return Task.CompletedTask;
    }
}

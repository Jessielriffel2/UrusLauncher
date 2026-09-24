using System.Net;
using System.Text;
using LegendLauncher.Core.Models;
using LegendLauncher.Providers.Elarionis;
using LegendLauncher.Tests.Oas;

namespace LegendLauncher.Tests.Elarionis;

public sealed class ElarionisAuthenticationServiceTests
{
    private static readonly Uri S7PlayUri = new("https://elarionis.online/s7/play?site=s7");

    [Fact]
    public async Task AuthenticateAsync_LogsInChecksGateAndResolvesSwfWithToken()
    {
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            var uri = request.RequestUri!;
            if (uri.AbsolutePath == "/api/login")
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                return Task.FromResult(JsonResponse("""{"ok":true,"token":"tok-12345678"}"""));
            }

            if (uri.AbsolutePath == "/s7/api/can_enter")
            {
                Assert.Contains("token=tok-12345678", uri.Query, StringComparison.Ordinal);
                return Task.FromResult(JsonResponse("""{"ok":true}"""));
            }

            Assert.StartsWith("https://elarionis.online/s7/play", uri.AbsoluteUri, StringComparison.Ordinal);
            Assert.Contains("token=tok-12345678", uri.Query, StringComparison.Ordinal);
            return Task.FromResult(HtmlResponse(
                """<html><body><embed src="https://elarionis.online/client/Loading.swf" /></body></html>"""));
        });
        var service = CreateService(handler);

        AuthenticationResult result = await service.AuthenticateAsync(CreateRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal(new Uri("https://elarionis.online/client/Loading.swf?token=tok-12345678"), result.Session?.LaunchUri);
        Assert.NotNull(result.Session?.Parameters);
        Assert.Equal("tok-12345678", result.Session!.Parameters["token"]);
        Assert.Equal("s7", result.Session.Parameters["site"]);
        Assert.Null(result.ProviderUserId);
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task AuthenticateAsync_ForwardsLoginCookiesLikeTheBrowserFlow()
    {
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            var uri = request.RequestUri!;
            if (uri.AbsolutePath == "/api/login")
            {
                var login = JsonResponse("""{"ok":true,"token":"tok-12345678"}""");
                login.Headers.TryAddWithoutValidation("Set-Cookie", "elarionis_session=abc123; Path=/; HttpOnly");
                return Task.FromResult(login);
            }

            Assert.True(
                request.Headers.TryGetValues("Cookie", out var cookies),
                "can_enter and play must carry the login cookie jar.");
            Assert.Contains("elarionis_session=abc123", string.Join(";", cookies), StringComparison.Ordinal);

            if (uri.AbsolutePath == "/s7/api/can_enter")
            {
                return Task.FromResult(JsonResponse("""{"ok":true}"""));
            }

            return Task.FromResult(HtmlResponse(
                """<html><body><embed src="https://elarionis.online/client/Loading.swf" /></body></html>"""));
        });
        var service = CreateService(handler);

        AuthenticationResult result = await service.AuthenticateAsync(CreateRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task AuthenticateAsync_RejectsEmptyCredentialsWithoutNetwork()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            throw new InvalidOperationException("Network must not be used."));
        var service = CreateService(handler);

        AuthenticationResult result = await service.AuthenticateAsync(
            CreateRequest(userName: "", password: ""));

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_credentials", result.ErrorCode);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task AuthenticateAsync_SurfacesServerLoginRejection()
    {
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            Assert.Equal("/api/login", request.RequestUri?.AbsolutePath);
            return Task.FromResult(JsonResponse("""{"ok":false,"error":"Boyle bir hesap yok"}"""));
        });
        var service = CreateService(handler);

        AuthenticationResult result = await service.AuthenticateAsync(CreateRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal("authentication_rejected", result.ErrorCode);
        Assert.Contains("Boyle bir hesap yok", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task AuthenticateAsync_MapsReloginGateToRejection()
    {
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri?.AbsolutePath == "/api/login")
            {
                return Task.FromResult(JsonResponse("""{"ok":true,"token":"tok-12345678"}"""));
            }

            return Task.FromResult(JsonResponse("""{"ok":false,"relogin":true}"""));
        });
        var service = CreateService(handler);

        AuthenticationResult result = await service.AuthenticateAsync(CreateRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal("authentication_rejected", result.ErrorCode);
    }

    [Fact]
    public async Task AuthenticateAsync_MapsFullServerGateToInvalidServer()
    {
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri?.AbsolutePath == "/api/login")
            {
                return Task.FromResult(JsonResponse("""{"ok":true,"token":"tok-12345678"}"""));
            }

            return Task.FromResult(JsonResponse("""{"ok":true,"full":true,"limit":5}"""));
        });
        var service = CreateService(handler);

        AuthenticationResult result = await service.AuthenticateAsync(CreateRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_server", result.ErrorCode);
    }

    [Fact]
    public async Task AuthenticateAsync_RejectsPlayPageWithoutMovie()
    {
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri?.AbsolutePath == "/api/login")
            {
                return Task.FromResult(JsonResponse("""{"ok":true,"token":"tok-12345678"}"""));
            }

            if (request.RequestUri?.AbsolutePath == "/s7/api/can_enter")
            {
                return Task.FromResult(JsonResponse("""{"ok":true}"""));
            }

            return Task.FromResult(HtmlResponse("<html><body><p>Sunucu Dolu</p></body></html>"));
        });
        var service = CreateService(handler);

        AuthenticationResult result = await service.AuthenticateAsync(CreateRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_launch_response", result.ErrorCode);
    }

    [Fact]
    public async Task AuthenticateAsync_RejectsSwfOutsideAllowlist()
    {
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri?.AbsolutePath == "/api/login")
            {
                return Task.FromResult(JsonResponse("""{"ok":true,"token":"tok-12345678"}"""));
            }

            if (request.RequestUri?.AbsolutePath == "/s7/api/can_enter")
            {
                return Task.FromResult(JsonResponse("""{"ok":true}"""));
            }

            return Task.FromResult(HtmlResponse(
                """<html><body><embed src="https://attacker.example/game.swf" /></body></html>"""));
        });
        var service = CreateService(handler);

        AuthenticationResult result = await service.AuthenticateAsync(CreateRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal("origin_not_allowed", result.ErrorCode);
    }

    [Fact]
    public async Task AuthenticateAsync_RejectsTamperedPlatformBeforeNetwork()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            throw new InvalidOperationException("Network must not be used."));
        var service = CreateService(handler);
        PlatformDefinition tampered = ElarionisPlatformCatalog.Elarionis with
        {
            ServerListEndpoint = new Uri("https://attacker.example/api/shards"),
        };
        var server = new GameServer(
            "7", 7, "S7", "Kapadokya", "S7 - KAPADOKYA", S7PlayUri, false, true, null, null);

        AuthenticationResult result = await service.AuthenticateAsync(
            new AuthenticationRequest(
                tampered, server, "tester", new CredentialSecret("tester", "secret123")));

        Assert.False(result.IsSuccess);
        Assert.Equal("unsupported_platform", result.ErrorCode);
        Assert.Equal(0, handler.CallCount);
    }

    private static ElarionisAuthenticationService CreateService(StubHttpMessageHandler handler) =>
        new(() => handler, TimeSpan.FromSeconds(5));

    private static AuthenticationRequest CreateRequest(
        string userName = "tester",
        string password = "secret123")
    {
        var server = new GameServer(
            "7", 7, "S7", "Kapadokya", "S7 - KAPADOKYA", S7PlayUri, false, true, null, null);
        return new AuthenticationRequest(
            ElarionisPlatformCatalog.Elarionis,
            server,
            userName,
            new CredentialSecret(userName, password));
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private static HttpResponseMessage HtmlResponse(string html) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(html, Encoding.UTF8, "text/html"),
    };
}

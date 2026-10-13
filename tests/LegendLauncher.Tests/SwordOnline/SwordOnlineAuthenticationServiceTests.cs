using System.Net;
using System.Text;
using System.Text.Json;
using LegendLauncher.Core.Models;
using LegendLauncher.Providers.SwordOnline;
using LegendLauncher.Tests.Oas;

namespace LegendLauncher.Tests.SwordOnline;

public sealed class SwordOnlineAuthenticationServiceTests
{
    private static readonly string Ticket = new('a', 64);

    [Fact]
    public async Task AuthenticateAsync_PostsLoginAndPasswordAsJsonToTheLauncherEndpoint()
    {
        string? capturedBody = null;
        HttpRequestMessage? captured = null;
        var handler = new StubHttpMessageHandler(async (request, _) =>
        {
            captured = request;
            capturedBody = await request.Content!.ReadAsStringAsync();
            return Json(HttpStatusCode.OK, SuccessBody("Heroi"));
        });

        AuthenticationResult result = await Create(handler).AuthenticateAsync(Request("heroi", "Senha12345"));

        Assert.True(result.IsSuccess);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.Equal("https://swordonline.tech/api/launcher", captured.RequestUri!.AbsoluteUri);
        Assert.Equal("application/json", captured.Content!.Headers.ContentType!.MediaType);
        using JsonDocument body = JsonDocument.Parse(capturedBody!);
        Assert.Equal("heroi", body.RootElement.GetProperty("login").GetString());
        Assert.Equal("Senha12345", body.RootElement.GetProperty("senha").GetString());
    }

    [Fact]
    public async Task AuthenticateAsync_BuildsTheFlashSessionFromTheTicket()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            Json(HttpStatusCode.OK, SuccessBody("Heroi"))));

        AuthenticationResult result = await Create(handler).AuthenticateAsync(Request("heroi", "Senha12345"));

        Assert.True(result.IsSuccess);
        LaunchSession session = result.Session!;
        Assert.Equal("https://swordonline.tech/Loading.swf", session.LaunchUri.AbsoluteUri);
        // the portal's canonical spelling of the login wins over what was typed
        Assert.Equal("Heroi", session.Parameters["user"]);
        Assert.Equal(Ticket, session.Parameters["key"]);
        Assert.Equal("local", session.Parameters["site"]);
        Assert.Equal("https://swordonline.tech/config.xml", session.Parameters["config"]);
        Assert.Equal("0", session.Parameters["playfb"]);
        Assert.Equal("https:", session.Parameters["http"]);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "invalid_credentials")]
    [InlineData(HttpStatusCode.BadRequest, "authentication_rejected")]
    [InlineData(HttpStatusCode.TooManyRequests, "too_many_attempts")]
    [InlineData(HttpStatusCode.InternalServerError, "http_error")]
    public async Task AuthenticateAsync_MapsHttpFailuresToStableCodes(HttpStatusCode status, string expectedCode)
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            Json(status, "{\"ok\":false,\"erro\":\"Login ou senha incorretos.\"}")));

        AuthenticationResult result = await Create(handler).AuthenticateAsync(Request("heroi", "Senha12345"));

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedCode, result.ErrorCode);
        Assert.Equal("Login ou senha incorretos.", result.ErrorMessage);
    }

    [Fact]
    public async Task AuthenticateAsync_SendsAnEmailAndPlaysWithTheCanonicalLogin()
    {
        string? sentLogin = null;
        var handler = new StubHttpMessageHandler(async (request, _) =>
        {
            using JsonDocument sent = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            sentLogin = sent.RootElement.GetProperty("login").GetString();
            return Json(HttpStatusCode.OK, SuccessBody("GrocH"));
        });

        // The portal accepts the e-mail OR the login and answers with the account login.
        AuthenticationResult result = await Create(handler).AuthenticateAsync(Request("groch@gmail.com", "Senha12345"));

        Assert.True(result.IsSuccess);
        Assert.Equal("groch@gmail.com", sentLogin);
        Assert.Equal("GrocH", result.Session!.Parameters["user"]);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"ok\":true}")]
    [InlineData("{\"ok\":true,\"ficha\":\"curta\"}")]
    [InlineData("{\"ok\":false,\"ficha\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}")]
    public async Task AuthenticateAsync_RejectsAMalformedSuccessBody(string body)
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK, body)));

        AuthenticationResult result = await Create(handler).AuthenticateAsync(Request("heroi", "Senha12345"));

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_authentication_response", result.ErrorCode);
    }

    [Fact]
    public async Task AuthenticateAsync_RejectsAnOversizedResponse()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            Json(HttpStatusCode.OK, new string('x', 20 * 1024))));

        AuthenticationResult result = await Create(handler).AuthenticateAsync(Request("heroi", "Senha12345"));

        Assert.Equal("response_too_large", result.ErrorCode);
    }

    [Fact]
    public async Task AuthenticateAsync_ReportsNetworkFailureWithoutLeakingThePassword()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            throw new HttpRequestException("falha com Senha12345"));

        AuthenticationResult result = await Create(handler).AuthenticateAsync(Request("heroi", "Senha12345"));

        Assert.Equal("network_error", result.ErrorCode);
        Assert.DoesNotContain("Senha12345", result.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Senha12345", result.ErrorMessage ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthenticateAsync_PropagatesCallerCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var handler = new StubHttpMessageHandler((_, token) =>
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(Json(HttpStatusCode.OK, "{}"));
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Create(handler).AuthenticateAsync(Request("heroi", "Senha12345"), cts.Token));
    }

    [Theory]
    [InlineData("", "Senha12345")]
    [InlineData("heroi", "")]
    public async Task AuthenticateAsync_RejectsEmptyCredentialsWithoutCallingTheNetwork(string login, string password)
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK, "{}")));

        AuthenticationResult result = await Create(handler).AuthenticateAsync(Request(login, password));

        Assert.Equal("invalid_credentials", result.ErrorCode);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task AuthenticateAsync_RejectsForeignPlatformAndServer()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK, "{}")));
        SwordOnlineAuthenticationService service = Create(handler);
        PlatformDefinition other = SwordOnlinePlatformCatalog.SwordOnline with { Id = "other" };

        AuthenticationResult wrongPlatform = await service.AuthenticateAsync(
            new AuthenticationRequest(other, Server(), "heroi", new CredentialSecret("heroi", "Senha12345")));
        AuthenticationResult wrongServer = await service.AuthenticateAsync(
            new AuthenticationRequest(
                SwordOnlinePlatformCatalog.SwordOnline,
                Server() with { Id = "x" },
                "heroi",
                new CredentialSecret("heroi", "Senha12345")));

        Assert.Equal("unsupported_platform", wrongPlatform.ErrorCode);
        Assert.Equal("invalid_server", wrongServer.ErrorCode);
        Assert.Equal(0, handler.CallCount);
    }

    private static string SuccessBody(string login) =>
        "{\"ok\":true,\"login\":\"" + login + "\",\"ficha\":\"" + Ticket + "\"}";

    private static SwordOnlineAuthenticationService Create(HttpMessageHandler handler) =>
        new(new HttpClient(handler));

    private static GameServer Server() => new(
        SwordOnlineServerDirectory.ServerId,
        1,
        "swo",
        "SwordOnline",
        "SwordOnline (swordonline.tech)",
        SwordOnlinePlatformCatalog.MovieUri,
        true,
        true,
        null,
        null);

    private static AuthenticationRequest Request(string login, string password) => new(
        SwordOnlinePlatformCatalog.SwordOnline,
        Server(),
        login,
        new CredentialSecret(login, password));

    private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };
}

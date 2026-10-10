using System.Net;
using System.Text;
using System.Text.Json;
using LegendLauncher.Core.Models;
using LegendLauncher.Providers.Oas;
using LegendLauncher.Providers.SwordOnline;
using LegendLauncher.Tests.Oas;

namespace LegendLauncher.Tests.SwordOnline;

public sealed class SwordOnlineAccountRegistrationServiceTests
{
    [Fact]
    public async Task RegisterAsync_PostsLoginPasswordAndEmailAsJsonToTheRegisterEndpoint()
    {
        string? capturedBody = null;
        HttpRequestMessage? captured = null;
        var handler = new StubHttpMessageHandler(async (request, _) =>
        {
            captured = request;
            capturedBody = await request.Content!.ReadAsStringAsync();
            return Json(HttpStatusCode.OK, "{\"ok\":true}");
        });

        AccountRegistrationResult result = await Create(handler)
            .RegisterAsync(Request("  Heroi_01 ", "Senha12345", " heroi@exemplo.com "));

        Assert.True(result.IsSuccess);
        Assert.Equal("Heroi_01", result.Login);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.Equal("https://swordonline.tech/api/conta/criar", captured.RequestUri!.AbsoluteUri);
        Assert.Equal("application/json", captured.Content!.Headers.ContentType!.MediaType);
        using JsonDocument body = JsonDocument.Parse(capturedBody!);
        Assert.Equal("Heroi_01", body.RootElement.GetProperty("login").GetString());
        Assert.Equal("Senha12345", body.RootElement.GetProperty("senha").GetString());
        Assert.Equal("heroi@exemplo.com", body.RootElement.GetProperty("email").GetString());
    }

    [Fact]
    public async Task RegisterAsync_SendsAnEmptyEmailWhenNoneWasGiven()
    {
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return Json(HttpStatusCode.OK, "{\"ok\":true}");
        });

        AccountRegistrationResult result = await Create(handler)
            .RegisterAsync(Request("Heroi_01", "Senha12345"));

        Assert.True(result.IsSuccess);
        using JsonDocument body = JsonDocument.Parse(capturedBody!);
        Assert.Equal(string.Empty, body.RootElement.GetProperty("email").GetString());
    }

    [Fact]
    public async Task RegisterAsync_PrefersTheCanonicalLoginTheSiteAnswersWith()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            Json(HttpStatusCode.Created, "{\"ok\":true,\"login\":\"HEROI_01\"}")));

        AccountRegistrationResult result = await Create(handler)
            .RegisterAsync(Request("heroi_01", "Senha12345"));

        Assert.True(result.IsSuccess);
        Assert.Equal("HEROI_01", result.Login);
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, "registration_login_taken")]
    [InlineData(HttpStatusCode.TooManyRequests, "registration_too_many_requests")]
    [InlineData(HttpStatusCode.BadRequest, "registration_rejected")]
    [InlineData(HttpStatusCode.InternalServerError, "http_error")]
    public async Task RegisterAsync_MapsHttpFailuresToStableCodes(HttpStatusCode status, string expectedCode)
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            Json(status, "{\"ok\":false,\"erro\":\"Esse login já existe.\"}")));

        AccountRegistrationResult result = await Create(handler)
            .RegisterAsync(Request("Heroi_01", "Senha12345"));

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedCode, result.ErrorCode);
        Assert.Equal("Esse login já existe.", result.ErrorMessage);
        Assert.Null(result.Login);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"ok\":false}")]
    public async Task RegisterAsync_RejectsAMalformedSuccessBody(string body)
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK, body)));

        AccountRegistrationResult result = await Create(handler)
            .RegisterAsync(Request("Heroi_01", "Senha12345"));

        Assert.False(result.IsSuccess);
        Assert.Equal(SwordOnlineAuthenticationErrorCodes.InvalidAuthenticationResponse, result.ErrorCode);
    }

    [Theory]
    [InlineData("abc", "registration_invalid_login")]
    [InlineData("1heroi", "registration_invalid_login")]
    [InlineData("heroi com espaco", "registration_invalid_login")]
    [InlineData("heroi-01", "registration_invalid_login")]
    [InlineData("heroi_com_mais_de_16", "registration_invalid_login")]
    public async Task RegisterAsync_RejectsAnInvalidLoginWithoutTouchingTheNetwork(string login, string expectedCode)
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK, "{\"ok\":true}")));

        AccountRegistrationResult result = await Create(handler)
            .RegisterAsync(Request(login.TrimEnd(' '), "Senha12345"));

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedCode, result.ErrorCode);
        Assert.Equal(0, handler.CallCount);
    }

    [Theory]
    [InlineData("curta1")]
    [InlineData("somenteletras")]
    [InlineData("1234567890")]
    public async Task RegisterAsync_RejectsAWeakPasswordWithoutTouchingTheNetwork(string password)
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK, "{\"ok\":true}")));

        AccountRegistrationResult result = await Create(handler)
            .RegisterAsync(Request("Heroi_01", password));

        Assert.False(result.IsSuccess);
        Assert.Equal(SwordOnlineRegistrationErrorCodes.InvalidPassword, result.ErrorCode);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task RegisterAsync_RejectsAPasswordLongerThanTheSiteAllows()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK, "{\"ok\":true}")));

        AccountRegistrationResult result = await Create(handler)
            .RegisterAsync(Request("Heroi_01", new string('a', 64) + "1"));

        Assert.Equal(SwordOnlineRegistrationErrorCodes.InvalidPassword, result.ErrorCode);
        Assert.Equal(0, handler.CallCount);
    }

    [Theory]
    [InlineData("sem-arroba")]
    [InlineData("@semusuario.com")]
    [InlineData("dois@@exemplo.com")]
    [InlineData("com espaco@exemplo.com")]
    public async Task RegisterAsync_RejectsAnObviouslyInvalidEmail(string email)
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK, "{\"ok\":true}")));

        AccountRegistrationResult result = await Create(handler)
            .RegisterAsync(Request("Heroi_01", "Senha12345", email));

        Assert.Equal(SwordOnlineRegistrationErrorCodes.InvalidEmail, result.ErrorCode);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task RegisterAsync_RefusesAnotherPlatformWithoutTouchingTheNetwork()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK, "{\"ok\":true}")));
        var request = new AccountRegistrationRequest(OasPlatformCatalog.All[0], "Heroi_01", "Senha12345");

        AccountRegistrationResult result = await Create(handler).RegisterAsync(request);

        Assert.Equal(SwordOnlineAuthenticationErrorCodes.UnsupportedPlatform, result.ErrorCode);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task RegisterAsync_ReportsNetworkFailureWithoutLeakingThePassword()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            throw new HttpRequestException("falha ao falar com swordonline.tech senha=Senha12345"));

        AccountRegistrationResult result = await Create(handler)
            .RegisterAsync(Request("Heroi_01", "Senha12345"));

        Assert.False(result.IsSuccess);
        Assert.Equal(SwordOnlineAuthenticationErrorCodes.NetworkError, result.ErrorCode);
        Assert.DoesNotContain("Senha12345", result.ToString());
        Assert.DoesNotContain("Senha12345", result.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public async Task RegisterAsync_RejectsAResponseLargerThanTheLimit()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            Json(HttpStatusCode.OK, new string('x', 20 * 1024))));

        AccountRegistrationResult result = await Create(handler)
            .RegisterAsync(Request("Heroi_01", "Senha12345"));

        Assert.Equal(SwordOnlineAuthenticationErrorCodes.ResponseTooLarge, result.ErrorCode);
    }

    [Fact]
    public async Task RegisterAsync_PropagatesCallerCancellation()
    {
        using var cts = new CancellationTokenSource();
        var handler = new StubHttpMessageHandler((_, token) =>
        {
            cts.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.FromResult(Json(HttpStatusCode.OK, "{\"ok\":true}"));
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Create(handler).RegisterAsync(Request("Heroi_01", "Senha12345"), cts.Token));
    }

    [Fact]
    public void Request_AndResult_NeverPrintTheirSecrets()
    {
        AccountRegistrationRequest request = Request("Heroi_01", "Senha12345", "heroi@exemplo.com");

        Assert.DoesNotContain("Senha12345", request.ToString());
        Assert.DoesNotContain("heroi@exemplo.com", request.ToString());
    }

    [Fact]
    public void SwordOnlinePlatform_AdvertisesInAppAccountCreation()
    {
        Assert.True(SwordOnlinePlatformCatalog.SwordOnline.SupportsAccountCreation);
        Assert.Equal(
            "https://swordonline.tech/api/conta/criar",
            SwordOnlinePlatformCatalog.RegisterUri.AbsoluteUri);
    }

    private static SwordOnlineAccountRegistrationService Create(HttpMessageHandler handler) =>
        new(new HttpClient(handler));

    private static AccountRegistrationRequest Request(string login, string password, string? email = null) =>
        new(SwordOnlinePlatformCatalog.SwordOnline, login, password, email);

    private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };
}

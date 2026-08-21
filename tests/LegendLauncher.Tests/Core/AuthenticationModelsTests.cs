using LegendLauncher.Core.Models;
using LegendLauncher.Providers.Oas;

namespace LegendLauncher.Tests.Core;

public sealed class AuthenticationModelsTests
{
    [Fact]
    public void ToString_DoesNotExposePasswordOrLaunchUriSecrets()
    {
        const string password = "segredo-que-nao-pode-vazar";
        const string token = "token-que-nao-pode-vazar";
        var credential = new CredentialSecret("conta@example.test", password);
        var request = new AuthenticationRequest(
            OasPlatformCatalog.Brazil,
            new GameServer("1", "Servidor 1"),
            password,
            credential);
        var session = new LaunchSession(
            new Uri($"https://game.example/launch?token={token}"),
            new Dictionary<string, string> { ["token"] = token });
        var result = AuthenticationResult.Success(session, 987654321);

        Assert.DoesNotContain(password, credential.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(password, request.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(token, session.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("game.example", session.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(token, result.ToString(), StringComparison.Ordinal);
        Assert.Equal(987654321, result.ProviderUserId);
        Assert.Contains("HasProviderUserId = True", result.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void FailureDiagnosticContainsOnlyBoundedMetadata()
    {
        const string remoteSecret = "secret-that-must-not-appear";
        var diagnostic = new AuthenticationFailureDiagnostic(
            AuthenticationFailurePhase.Passport,
            AuthenticationTransportKind.SystemCurl,
            403);
        var result = AuthenticationResult.Failure(
            "http_error",
            $"Remote text omitted: {remoteSecret}".Replace(remoteSecret, "sanitized", StringComparison.Ordinal),
            diagnostic);

        Assert.Equal(AuthenticationFailurePhase.Passport, result.FailureDiagnostic?.Phase);
        Assert.Equal(AuthenticationTransportKind.SystemCurl, result.FailureDiagnostic?.Transport);
        Assert.Equal(403, result.FailureDiagnostic?.HttpStatusCode);
        Assert.DoesNotContain(remoteSecret, diagnostic.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(remoteSecret, result.ToString(), StringComparison.Ordinal);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AuthenticationFailureDiagnostic(
                AuthenticationFailurePhase.Launch,
                AuthenticationTransportKind.ManagedHttp,
                42));
    }
}

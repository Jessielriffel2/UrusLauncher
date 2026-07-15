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
}

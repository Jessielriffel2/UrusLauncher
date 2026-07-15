using LegendLauncher.Core.Models;
using LegendLauncher.Infrastructure.Paths;
using LegendLauncher.Infrastructure.Persistence;
using LegendLauncher.Infrastructure.Security;
using LegendLauncher.Providers.Oas;

namespace LegendLauncher.Tests.Oas;

public sealed class OasLiveSmokeTests
{
    private const string OptInVariable = "LEGEND_OAS_LIVE_SMOKE";

    [Fact]
    public async Task RebornTurkishS115_ResolvesStoredProfileWhenExplicitlyEnabled()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable(OptInVariable),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        var paths = new AppPaths();
        var profiles = await new JsonProfileStore(paths.ProfilesFile).GetAllAsync();
        var profile = Assert.Single(profiles, item => string.Equals(
            item.PlatformId,
            OasPlatformCatalog.RebornTurkish.Id,
            StringComparison.Ordinal));
        var secret = await new WindowsCredentialVault().GetAsync(profile.CredentialKey);
        Assert.NotNull(secret);

        var server = new GameServer(
            "115",
            115,
            "S115",
            "Rüzgar Dalgası",
            "S115: Rüzgar Dalgası",
            new Uri("https://lortr.creaction-network.com/serverlist/s115"),
            IsRecommended: false,
            IsValid: true,
            Merger: null,
            StartTimeUtc: null);
        var request = new AuthenticationRequest(
            OasPlatformCatalog.RebornTurkish,
            server,
            profile.UserName,
            secret);

        var result = await new OasAuthenticationService().AuthenticateAsync(request);

        Assert.True(
            result.IsSuccess,
            $"Live OAS smoke failed with safe error code '{result.ErrorCode}'.");
        Assert.True(result.ProviderUserId > 0);
        Assert.NotNull(result.Session);
        Assert.Equal("/client/Loading.swf", result.Session.LaunchUri.AbsolutePath);
        Assert.EndsWith(
            ".creaction-network.com",
            result.Session.LaunchUri.IdnHost,
            StringComparison.OrdinalIgnoreCase);
    }
}

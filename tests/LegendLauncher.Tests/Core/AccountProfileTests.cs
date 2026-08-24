using LegendLauncher.Core.Models;
using LegendLauncher.Infrastructure.Security;

namespace LegendLauncher.Tests.Core;

public sealed class AccountProfileTests
{
    [Fact]
    public void GetRecentServerIds_FallsBackToLastServerWhenPlatformMapIsEmpty()
    {
        Guid profileId = Guid.NewGuid();
        var profile = new AccountProfile(
            profileId,
            "TIAGO",
            "oas-lobr",
            "tiago@example.test",
            CredentialKey.ForProfile(profileId),
            42,
            "100",
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch)
        {
            RecentServerIds = [],
            RecentServerIdsByPlatform = new Dictionary<string, IReadOnlyList<string>>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["oas-lobr"] = [],
            },
        };

        Assert.Equal(["100"], profile.GetRecentServerIds("OAS-LOBR"));
        Assert.Equal("100", profile.GetLastServerId("oas-lobr"));
        Assert.Empty(profile.GetRecentServerIds("oas-lorpt"));
    }
}

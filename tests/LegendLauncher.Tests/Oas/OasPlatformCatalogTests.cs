using LegendLauncher.Core.Models;
using LegendLauncher.Providers.Oas;

namespace LegendLauncher.Tests.Oas;

public sealed class OasPlatformCatalogTests
{
    private static readonly Uri ExpectedEndpoint =
        new("https://odp3.oasgames.com/api/game/serverlist", UriKind.Absolute);

    public static TheoryData<
        PlatformDefinition,
        string,
        string,
        string,
        string> SupportedPlatforms => new()
        {
            {
                OasPlatformCatalog.Brazil,
                "oas-lobr",
                "Legend Online Brasil (OAS)",
                "lobr",
                "pt-BR"
            },
            {
                OasPlatformCatalog.Turkish,
                "oas-lotr",
                "Legend Online Türkçe (OAS)",
                "lotr",
                "tr-TR"
            },
            {
                OasPlatformCatalog.ClassicPortuguese,
                "oas-lorpt",
                "Legend Online Classic Português (OAS)",
                "lorpt",
                "pt-PT"
            },
            {
                OasPlatformCatalog.RebornTurkish,
                "oas-lortr",
                "Legend Online Reborn Türkçe (OAS)",
                "lortr",
                "tr-TR"
            },
            {
                OasPlatformCatalog.Polish,
                "oas-lopl",
                "Legend Online Polska (OAS)",
                "lopl",
                "pl-PL"
            },
            {
                OasPlatformCatalog.Spanish,
                "oas-loes",
                "Legend Online Español (OAS)",
                "loes",
                "es-ES"
            },
            {
                OasPlatformCatalog.German,
                "oas-lode",
                "Legend Online Deutsch (OAS)",
                "lode",
                "de-DE"
            },
            {
                OasPlatformCatalog.Arabic,
                "oas-loar",
                "Legend Online عربي (OAS)",
                "loar",
                "ar"
            },
        };

    [Theory]
    [MemberData(nameof(SupportedPlatforms))]
    public void SupportedPlatform_HasExpectedVerifiedDefinition(
        PlatformDefinition platform,
        string expectedId,
        string expectedDisplayName,
        string expectedGameCode,
        string expectedLocale)
    {
        Assert.Equal(expectedId, platform.Id);
        Assert.Equal(expectedDisplayName, platform.DisplayName);
        Assert.Equal(expectedGameCode, platform.GameCode);
        Assert.Equal(ExpectedEndpoint, platform.ServerListEndpoint);
        Assert.Equal(expectedLocale, platform.Locale);
    }

    [Fact]
    public void All_ContainsEachSupportedPlatformOnceInLauncherOrder()
    {
        string[] expectedIds =
        [
            "oas-lobr",
            "oas-lotr",
            "oas-lorpt",
            "oas-lortr",
            "oas-lopl",
            "oas-loes",
            "oas-lode",
            "oas-loar",
        ];

        Assert.Equal(expectedIds, OasPlatformCatalog.All.Select(platform => platform.Id));
        Assert.Equal(
            OasPlatformCatalog.All.Count,
            OasPlatformCatalog.All.Select(platform => platform.GameCode).Distinct().Count());
    }

    [Theory]
    [InlineData("OAS-LOBR", "lobr")]
    [InlineData("oas-lotr", "lotr")]
    [InlineData("OAS-LORPT", "lorpt")]
    [InlineData("oas-lortr", "lortr")]
    [InlineData("OAS-LOPL", "lopl")]
    [InlineData("oas-loes", "loes")]
    [InlineData("OAS-LODE", "lode")]
    [InlineData("oas-loar", "loar")]
    public void Find_UsesCaseInsensitivePlatformIdentity(
        string platformId,
        string expectedGameCode)
    {
        PlatformDefinition platform = Assert.IsType<PlatformDefinition>(
            OasPlatformCatalog.Find(platformId));

        Assert.Equal(expectedGameCode, platform.GameCode);
    }

    [Fact]
    public void Find_ReturnsNullForUnknownPlatform()
    {
        Assert.Null(OasPlatformCatalog.Find("oas-unknown"));
    }

    [Fact]
    public void All_CannotBeMutatedThroughListInterface()
    {
        var list = Assert.IsAssignableFrom<IList<PlatformDefinition>>(OasPlatformCatalog.All);

        Assert.Throws<NotSupportedException>(() => list.Add(OasPlatformCatalog.Brazil));
    }
}

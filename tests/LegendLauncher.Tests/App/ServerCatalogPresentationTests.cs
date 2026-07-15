using LegendLauncher.App.Services;
using LegendLauncher.App.ViewModels;
using LegendLauncher.Core.Models;

namespace LegendLauncher.Tests.App;

public sealed class ServerCatalogPresentationTests
{
    [Fact]
    public void ResolveLastPlayedServerId_PrefersHistoryAndFallsBackForLegacyProfile()
    {
        AccountProfile profile = AppTestData.Profile("player@example.test", null, "100") with
        {
            RecentServerIds = [" 200 ", "100"],
        };

        Assert.Equal(
            "200",
            ServerCatalogPresentation.ResolveLastPlayedServerId(profile, profile.PlatformId));
        Assert.Equal(
            "100",
            ServerCatalogPresentation.ResolveLastPlayedServerId(
                profile with { RecentServerIds = [] },
                profile.PlatformId));
        Assert.Null(ServerCatalogPresentation.ResolveLastPlayedServerId(profile, "other-platform"));
    }

    [Fact]
    public void BuildRows_PinsLastUsedThenSeparatesNewestFromRemainingServers()
    {
        GameServer current = AppTestData.Server("100", opensAt: AppTestData.Now.AddDays(-10));
        GameServer catalogRecommended = AppTestData.Server(
            "200",
            recommended: true,
            opensAt: AppTestData.Now.AddDays(-4));
        GameServer played = AppTestData.Server("300", opensAt: AppTestData.Now.AddDays(-3));
        GameServer newest = AppTestData.Server("400", opensAt: AppTestData.Now.AddHours(-1));
        ServerCatalog catalog = AppTestData.Catalog(
            [newest, played, catalogRecommended, current],
            [played],
            current);

        IReadOnlyList<ServerRowViewModel> rows =
            ServerCatalogPresentation.BuildRows(catalog, AppTestData.Now);

        Assert.Equal(["100", "400", "300", "200"], rows.Select(row => row.Id));
        Assert.Equal("RECOMENDADO", rows[0].RecommendedBadgeText);
        Assert.True(rows[0].ShowRecommendedBadge);
        Assert.True(rows[1].ShowLatestBadge);
        Assert.Equal("MAIS RECENTE", rows[1].LatestBadgeText);
        Assert.True(rows[1].ShowSectionDivider);
        Assert.False(rows[3].ShowRecommendedBadge);
        Assert.False(rows[3].ShowLatestBadge);
        Assert.True(rows[2].IsPreviouslyPlayed);
    }

    [Fact]
    public void Choose_SkipsUnsafeAndUnavailableServers()
    {
        GameServer unsafeServer = AppTestData.Server("100") with { LaunchUri = null };
        GameServer unavailable = AppTestData.Server("200", valid: false);
        GameServer recommended = AppTestData.Server("300", recommended: true);
        IReadOnlyList<ServerRowViewModel> rows =
            ServerCatalogPresentation.BuildRows(
                AppTestData.Catalog([unsafeServer, unavailable, recommended]),
                AppTestData.Now);

        ServerRowViewModel? chosen = ServerCatalogPresentation.Choose(rows, desiredId: "100");

        Assert.Equal("300", chosen?.Id);
        Assert.False(rows.Single(row => row.Id == "100").CanLaunch);
        Assert.Equal(
            "Endereço seguro de jogo ausente",
            rows.Single(row => row.Id == "100").AvailabilityLabel);
    }

    [Fact]
    public void BuildRows_ProfileLastServerOverridesMissingCachedHistory()
    {
        ServerCatalog cachedCatalog = AppTestData.Catalog(
            [AppTestData.Server("100"), AppTestData.Server("200")]);

        IReadOnlyList<ServerRowViewModel> rows = ServerCatalogPresentation.BuildRows(
            cachedCatalog,
            AppTestData.Now,
            accountLastServerId: "100");

        ServerRowViewModel lastServer = rows.Single(row => row.Id == "100");
        Assert.True(lastServer.IsCurrent);
        Assert.True(lastServer.ShowRecommendedBadge);
        Assert.Equal("Seu último servidor · disponível", lastServer.AvailabilityLabel);
        Assert.Equal("100", rows[0].Id);
        Assert.True(rows[1].ShowLatestBadge);
        Assert.True(rows[1].ShowSectionDivider);
    }

    [Fact]
    public void Filter_MatchesCodeNameAndNumericId()
    {
        IReadOnlyList<ServerRowViewModel> rows =
            ServerCatalogPresentation.BuildRows(
                AppTestData.Catalog([AppTestData.Server("3257"), AppTestData.Server("2905")]),
                AppTestData.Now);

        Assert.Equal("3257", ServerCatalogPresentation.Filter(rows, "S3257").Single().Id);
        Assert.Equal("2905", ServerCatalogPresentation.Filter(rows, "2905").Single().Id);
        Assert.Empty(ServerCatalogPresentation.Filter(rows, "inexistente"));
    }

    [Fact]
    public void Filter_HidesSectionDividerWhenPinnedServerIsNotVisible()
    {
        GameServer current = AppTestData.Server("100");
        IReadOnlyList<ServerRowViewModel> rows = ServerCatalogPresentation.BuildRows(
            AppTestData.Catalog([current, AppTestData.Server("200")], current: current),
            AppTestData.Now);

        IReadOnlyList<ServerRowViewModel> filtered =
            ServerCatalogPresentation.Filter(rows, "S200");

        Assert.Single(filtered);
        Assert.False(filtered[0].ShowSectionDivider);
    }

    [Fact]
    public void BuildRows_NewestBadgeUsesOpeningDateAndSkipsFutureOrInvalidServers()
    {
        GameServer newestOpened = AppTestData.Server(
            "100",
            opensAt: AppTestData.Now.AddHours(-1));
        GameServer future = AppTestData.Server(
            "400",
            opensAt: AppTestData.Now.AddHours(1));
        GameServer invalid = AppTestData.Server(
            "300",
            valid: false,
            opensAt: AppTestData.Now.AddMinutes(-1));
        GameServer older = AppTestData.Server(
            "200",
            opensAt: AppTestData.Now.AddDays(-1));

        IReadOnlyList<ServerRowViewModel> rows = ServerCatalogPresentation.BuildRows(
            AppTestData.Catalog([future, invalid, older, newestOpened]),
            AppTestData.Now);

        Assert.Equal("100", rows.Single(row => row.ShowLatestBadge).Id);
        Assert.Equal("100", rows[0].Id);
    }

    [Fact]
    public void BuildRows_NewestBadgeFallsBackToNumericIdWhenDatesAreUnknown()
    {
        GameServer first = AppTestData.Server("100") with { StartTimeUtc = null };
        GameServer second = AppTestData.Server("200") with { StartTimeUtc = null };

        IReadOnlyList<ServerRowViewModel> rows = ServerCatalogPresentation.BuildRows(
            AppTestData.Catalog([first, second]),
            AppTestData.Now);

        Assert.Equal("200", rows.Single(row => row.ShowLatestBadge).Id);
    }

    [Fact]
    public void BuildRows_LastPlayedServerCanAlsoBeNewest()
    {
        GameServer newestAndCurrent = AppTestData.Server("200");

        ServerRowViewModel row = ServerCatalogPresentation.BuildRows(
            AppTestData.Catalog([AppTestData.Server("100"), newestAndCurrent], current: newestAndCurrent),
            AppTestData.Now)[0];

        Assert.True(row.ShowRecommendedBadge);
        Assert.True(row.ShowLatestBadge);
    }
}

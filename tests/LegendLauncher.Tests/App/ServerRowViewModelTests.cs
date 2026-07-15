using LegendLauncher.App.ViewModels;
using LegendLauncher.Core.Models;

namespace LegendLauncher.Tests.App;

public sealed class ServerRowViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 14, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void LastUsedAndNewestBadgesFollowPresentationRoles()
    {
        GameServer server = CreateServer(recommended: false, valid: true, opensAt: Now.AddDays(-1));

        var row = new ServerRowViewModel(
            server,
            Now,
            isCurrent: true,
            isLatestReleased: true);

        Assert.True(row.ShowRecommendedBadge);
        Assert.True(row.ShowLatestBadge);
        Assert.True(row.IsAvailable);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void RecommendedBadge_RepresentsLastUsedRoleEvenWhenServerIsUnavailable(
        bool isCurrent,
        bool expectedBadge)
    {
        GameServer server = CreateServer(
            recommended: false,
            valid: false,
            opensAt: Now.AddDays(1));

        var row = new ServerRowViewModel(server, Now, isCurrent: isCurrent);

        Assert.Equal(expectedBadge, row.ShowRecommendedBadge);
        Assert.False(row.CanLaunch);
    }

    private static GameServer CreateServer(
        bool recommended,
        bool valid,
        DateTimeOffset opensAt) =>
        new(
            Id: "3257",
            NumericId: 3257,
            Code: "S3257",
            Name: "Sombra sob a Lua",
            FullName: "OAS1257:Sombra sob a Lua",
            LaunchUri: new Uri("https://lobr.creaction-network.com/serverlist/s3257"),
            IsRecommended: recommended,
            IsValid: valid,
            Merger: null,
            StartTimeUtc: opensAt);
}

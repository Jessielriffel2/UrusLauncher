using LegendLauncher.App.ViewModels;
using LegendLauncher.Core.Models;
using LegendLauncher.Providers.SwordOnline;

namespace LegendLauncher.Tests.App;

public sealed class PlatformItemViewModelTests
{
    [Fact]
    public void IsHot_FollowsThePlatformDefinition()
    {
        PlatformDefinition ordinary = SwordOnlinePlatformCatalog.SwordOnline with { Id = "other", IsHot = false };

        Assert.True(new PlatformItemViewModel(SwordOnlinePlatformCatalog.SwordOnline).IsHot);
        Assert.False(new PlatformItemViewModel(ordinary).IsHot);
        Assert.False(new PlatformItemViewModel(PasswordlessTestPlatform.Platform).IsHot);
    }
}

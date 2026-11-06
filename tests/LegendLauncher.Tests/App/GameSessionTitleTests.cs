using LegendLauncher.App.ViewModels;
using LegendLauncher.Core.Models;
using LegendLauncher.Providers.SwordOnline;

namespace LegendLauncher.Tests.App;

public sealed class GameSessionTitleTests
{
    [Fact]
    public async Task SwordOnlineSession_ShowsTheS1MedivhNameWithoutRepeatingTheCode()
    {
        ServerCatalog catalog = await new SwordOnlineServerDirectory()
            .GetServersAsync(SwordOnlinePlatformCatalog.SwordOnline);
        using GameSessionViewModel session = Create(Assert.Single(catalog.All));

        Assert.Equal("Perfil · S1", session.TabTitle);
        Assert.Equal("Perfil · S1 - Medivh", session.DetachedTitle);
    }

    [Fact]
    public void DetachedTitle_KeepsTheCodeWhenTheNameDoesNotStartWithIt()
    {
        using GameSessionViewModel session = Create(
            new GameServer("7", 7, "S7", "Elarion", "Elarion", null, false, true, null, null));

        Assert.Equal("Perfil · S7 Elarion", session.DetachedTitle);
    }

    private static GameSessionViewModel Create(GameServer server)
    {
        Guid id = Guid.NewGuid();
        var profile = new AccountProfile(
            id,
            "Perfil",
            SwordOnlinePlatformCatalog.SwordOnline.Id,
            "player@example.test",
            $"LegendLauncherNext/profile/{id:N}",
            0,
            server.Id,
            AppTestData.Now,
            AppTestData.Now);
        return new GameSessionViewModel(
            profile,
            SwordOnlinePlatformCatalog.SwordOnline,
            server,
            new GameSession(900_001, (nint)0x1001, AppTestData.Now),
            attachment: null);
    }
}

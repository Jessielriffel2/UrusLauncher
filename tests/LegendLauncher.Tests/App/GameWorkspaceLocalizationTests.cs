using LegendLauncher.App.Localization;
using LegendLauncher.App.Services;
using LegendLauncher.App.ViewModels;
using LegendLauncher.Core.Models;
using LegendLauncher.Providers.Oas;

namespace LegendLauncher.Tests.App;

public sealed class GameWorkspaceLocalizationTests
{
    [Fact]
    public void LanguageSwitchUpdatesMuteLabelFooterAndRaisesBindingNotifications()
    {
        var localization = new LocalizationService("pt-BR");
        using GameWorkspaceViewModel workspace = CreateWorkspace(localization);
        var changedProperties = new List<string?>();
        workspace.PropertyChanged += (_, eventArgs) =>
            changedProperties.Add(eventArgs.PropertyName);
        _ = AddSession(workspace, 1);
        Assert.Equal("SOM GLOBAL: MUDO", workspace.MuteLabel);
        Assert.Equal(
            "1 sessão ativa  ·  Som global desativado  ·  Perfis isolados",
            workspace.FooterStatus);
        changedProperties.Clear();

        localization.SetLanguage("en-GB");

        Assert.Equal("GLOBAL SOUND: MUTED", workspace.MuteLabel);
        Assert.Equal(
            "1 active session  ·  Global sound muted  ·  Isolated profiles",
            workspace.FooterStatus);
        Assert.Contains(nameof(GameWorkspaceViewModel.MuteLabel), changedProperties);
        Assert.Contains(nameof(GameWorkspaceViewModel.FooterStatus), changedProperties);

        workspace.ToggleMuteCommand.Execute(null);
        localization.SetLanguage("es-MX");
        Assert.Equal("SONIDO GLOBAL: ACTIVO", workspace.MuteLabel);
        Assert.Equal(
            "1 sesión activa  ·  Sonido global activo  ·  Perfiles aislados",
            workspace.FooterStatus);
    }

    [Fact]
    public void FooterUsesLocalizedPluralForZeroAndMultipleSessions()
    {
        var localization = new LocalizationService("es-ES");
        using GameWorkspaceViewModel workspace = CreateWorkspace(localization);

        Assert.StartsWith("0 sesiones activas", workspace.FooterStatus);
        _ = AddSession(workspace, 1);
        _ = AddSession(workspace, 2);
        Assert.StartsWith("2 sesiones activas", workspace.FooterStatus);

        localization.SetLanguage("en-US");
        Assert.StartsWith("2 active sessions", workspace.FooterStatus);
    }

    [Fact]
    public void DisposeUnsubscribesFromLanguageChanges()
    {
        var localization = new LocalizationService("pt-BR");
        GameWorkspaceViewModel workspace = CreateWorkspace(localization);
        int localizationNotifications = 0;
        workspace.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName is nameof(GameWorkspaceViewModel.MuteLabel) or
                nameof(GameWorkspaceViewModel.FooterStatus))
            {
                localizationNotifications++;
            }
        };
        workspace.Dispose();

        localization.SetLanguage("en-US");

        Assert.Equal(0, localizationNotifications);
    }

    private static GameWorkspaceViewModel CreateWorkspace(LocalizationService localization)
    {
        var audio = new GameAudioService(static (_, _) => { }, TimeSpan.FromHours(1));
        return new GameWorkspaceViewModel(
            audio,
            new LauncherSettingsService(),
            static (_, _) => null,
            localization);
    }

    private static GameSessionViewModel AddSession(GameWorkspaceViewModel workspace, int index)
    {
        Guid id = Guid.NewGuid();
        var profile = new AccountProfile(
            id,
            $"Profile {index}",
            OasPlatformCatalog.Brazil.Id,
            $"player{index}@example.test",
            $"LegendLauncherNext/profile/{id:N}",
            index,
            index.ToString(System.Globalization.CultureInfo.InvariantCulture),
            AppTestData.Now,
            AppTestData.Now);
        var session = new GameSession(
            800_000 + index,
            (nint)(0x2000 + index),
            AppTestData.Now);
        return workspace.AddSession(
            profile,
            OasPlatformCatalog.Brazil,
            AppTestData.Server(index.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            session);
    }
}

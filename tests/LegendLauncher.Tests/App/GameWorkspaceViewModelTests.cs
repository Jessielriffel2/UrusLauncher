using LegendLauncher.App.Services;
using LegendLauncher.App.ViewModels;
using LegendLauncher.Core.Models;
using LegendLauncher.Providers.Oas;

namespace LegendLauncher.Tests.App;

public sealed class GameWorkspaceViewModelTests
{
    [Fact]
    public void GridFourAdaptsRowsToUseTheWholeSurfaceUntilAThirdSessionStarts()
    {
        using GameWorkspaceViewModel workspace = CreateWorkspace();

        _ = AddSession(workspace, 1);
        Assert.Equal(1, workspace.LayoutRows);
        Assert.Equal(1, workspace.LayoutColumns);

        _ = AddSession(workspace, 2);
        Assert.Equal(1, workspace.LayoutRows);
        Assert.Equal(2, workspace.LayoutColumns);

        _ = AddSession(workspace, 3);
        Assert.Equal(2, workspace.LayoutRows);
        Assert.Equal(2, workspace.LayoutColumns);
    }

    [Fact]
    public void LayoutsExposeOneTwoOrFourSessionsAndKeepSelectionVisible()
    {
        using GameWorkspaceViewModel workspace = CreateWorkspace();
        GameSessionViewModel[] sessions = Enumerable.Range(1, 5)
            .Select(index => AddSession(workspace, index))
            .ToArray();

        Assert.Equal(4, workspace.VisibleSessions.Count);
        workspace.SelectedSession = sessions[4];
        Assert.Contains(sessions[4], workspace.VisibleSessions);

        workspace.LayoutMode = GameLayoutMode.SplitTwo;
        Assert.Equal(2, workspace.VisibleSessions.Count);
        Assert.Contains(sessions[4], workspace.VisibleSessions);

        workspace.LayoutMode = GameLayoutMode.Single;
        Assert.Single(workspace.VisibleSessions);
        Assert.Same(sessions[4], workspace.VisibleSessions[0]);
    }

    [Fact]
    public void DetachAndReattachMoveTheSameSessionWithoutRemovingIt()
    {
        using GameWorkspaceViewModel workspace = CreateWorkspace();
        GameSessionViewModel session = AddSession(workspace, 1);
        GameSessionViewModel? requested = null;
        workspace.DetachRequested += (_, item) => requested = item;

        workspace.DetachSessionCommand.Execute(session);

        Assert.Same(session, requested);
        Assert.True(session.IsDetached);
        Assert.Empty(workspace.VisibleSessions);
        Assert.Single(workspace.Sessions);

        workspace.Reattach(session);
        Assert.False(session.IsDetached);
        Assert.Same(session, workspace.VisibleSessions.Single());
    }

    [Fact]
    public void ExistingSessionTargetIsFocusedInsteadOfDuplicated()
    {
        using GameWorkspaceViewModel workspace = CreateWorkspace();
        GameSessionViewModel first = AddSession(workspace, 1);
        _ = AddSession(workspace, 2);

        Assert.True(workspace.TryActivateSession(
            first.ProfileId,
            first.PlatformId,
            first.ServerId));
        Assert.Same(first, workspace.SelectedSession);
        Assert.Equal(2, workspace.Sessions.Count);
    }

    [Fact]
    public void SameProfileOnDifferentPlatformOrServerIsNotConfusedWithExistingSession()
    {
        using GameWorkspaceViewModel workspace = CreateWorkspace();
        Guid profileId = Guid.NewGuid();
        GameSessionViewModel brazilS100 = AddSession(
            workspace,
            1,
            profileId,
            OasPlatformCatalog.Brazil,
            "100");
        GameSessionViewModel classicS100 = AddSession(
            workspace,
            2,
            profileId,
            OasPlatformCatalog.ClassicPortuguese,
            "100");
        GameSessionViewModel classicS101 = AddSession(
            workspace,
            3,
            profileId,
            OasPlatformCatalog.ClassicPortuguese,
            "101");

        Assert.Equal(OasPlatformCatalog.Brazil.Id, brazilS100.PlatformId);
        Assert.Equal("100", brazilS100.ServerId);
        Assert.True(workspace.TryActivateSession(
            profileId,
            OasPlatformCatalog.ClassicPortuguese.Id,
            "100"));
        Assert.Same(classicS100, workspace.SelectedSession);

        Assert.True(workspace.TryActivateSession(
            profileId,
            OasPlatformCatalog.ClassicPortuguese.Id,
            "101"));
        Assert.Same(classicS101, workspace.SelectedSession);

        Assert.False(workspace.TryActivateSession(
            profileId,
            OasPlatformCatalog.Brazil.Id,
            "101"));
        Assert.Same(classicS101, workspace.SelectedSession);
    }

    [Fact]
    public void GlobalMuteAndClosingAffectOnlyWorkspaceState()
    {
        using var mutedSignal = new ManualResetEventSlim();
        using var audio = new GameAudioService(
            (_, muted) =>
            {
                if (!muted)
                {
                    mutedSignal.Set();
                }
            },
            TimeSpan.FromHours(1));
        var settings = new LauncherSettingsService();
        using var workspace = new GameWorkspaceViewModel(audio, settings, static (_, _) => null);
        GameSessionViewModel first = AddSession(workspace, 1);
        GameSessionViewModel second = AddSession(workspace, 2);

        workspace.ToggleMuteCommand.Execute(null);
        Assert.False(workspace.IsMuted);
        Assert.True(mutedSignal.Wait(TimeSpan.FromSeconds(2)));

        workspace.CloseSessionCommand.Execute(first);
        Assert.DoesNotContain(first, workspace.Sessions);
        Assert.Contains(second, workspace.Sessions);
    }

    private static GameWorkspaceViewModel CreateWorkspace()
    {
        var audio = new GameAudioService(static (_, _) => { }, TimeSpan.FromHours(1));
        return new GameWorkspaceViewModel(
            audio,
            new LauncherSettingsService(),
            static (_, _) => null);
    }

    private static GameSessionViewModel AddSession(
        GameWorkspaceViewModel workspace,
        int index,
        Guid? profileId = null,
        PlatformDefinition? platform = null,
        string? serverId = null)
    {
        Guid id = profileId ?? Guid.NewGuid();
        PlatformDefinition targetPlatform = platform ?? OasPlatformCatalog.Brazil;
        string targetServerId = serverId ??
            index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var profile = new AccountProfile(
            id,
            $"Perfil {index}",
            targetPlatform.Id,
            $"player{index}@example.test",
            $"LegendLauncherNext/profile/{id:N}",
            index,
            targetServerId,
            AppTestData.Now,
            AppTestData.Now);
        GameServer server = AppTestData.Server(targetServerId);
        var session = new GameSession(
            900_000 + index,
            (nint)(0x1000 + index),
            AppTestData.Now);
        return workspace.AddSession(profile, targetPlatform, server, session);
    }
}

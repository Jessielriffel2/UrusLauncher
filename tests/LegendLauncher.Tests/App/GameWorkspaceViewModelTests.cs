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
    public void ExistingProfileIsFocusedInsteadOfDuplicated()
    {
        using GameWorkspaceViewModel workspace = CreateWorkspace();
        GameSessionViewModel first = AddSession(workspace, 1);
        _ = AddSession(workspace, 2);

        Assert.True(workspace.TryActivateProfile(first.ProfileId));
        Assert.Same(first, workspace.SelectedSession);
        Assert.Equal(2, workspace.Sessions.Count);
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

    private static GameSessionViewModel AddSession(GameWorkspaceViewModel workspace, int index)
    {
        Guid id = Guid.NewGuid();
        var profile = new AccountProfile(
            id,
            $"Perfil {index}",
            OasPlatformCatalog.Brazil.Id,
            $"player{index}@example.test",
            $"LegendLauncherNext/profile/{id:N}",
            index,
            index.ToString(System.Globalization.CultureInfo.InvariantCulture),
            AppTestData.Now,
            AppTestData.Now);
        GameServer server = AppTestData.Server(index.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var session = new GameSession(
            900_000 + index,
            (nint)(0x1000 + index),
            AppTestData.Now);
        return workspace.AddSession(profile, OasPlatformCatalog.Brazil, server, session);
    }
}

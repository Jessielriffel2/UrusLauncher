using LegendLauncher.App;
using LegendLauncher.App.Views.Game;

namespace LegendLauncher.Tests.App;

public sealed class DetachedWindowLifecycleTests
{
    [Fact]
    public void CloseState_AllowsOnlyOneCloseAndOneReattachNotification()
    {
        var state = new DetachedWindowCloseState();

        Assert.True(state.TryBeginClose(suppressReattach: false));
        Assert.False(state.TryBeginClose(suppressReattach: false));
        Assert.True(state.TryRaiseReattach(sessionExists: true));
        Assert.False(state.TryRaiseReattach(sessionExists: true));
    }

    [Fact]
    public void CloseState_SuppressionPreventsReattach()
    {
        var state = new DetachedWindowCloseState();

        state.SuppressReattach();

        Assert.True(state.TryBeginClose(suppressReattach: true));
        Assert.False(state.TryRaiseReattach(sessionExists: true));
    }

    [Fact]
    public void TryShow_RollsBackAndRemovesRegistrationWhenShowFails()
    {
        Guid sessionId = Guid.NewGuid();
        var windows = new Dictionary<Guid, FakeWindow>();
        int initialized = 0;
        int cleaned = 0;
        int rolledBack = 0;

        bool shown = DetachedWindowCoordinator.TryShow(
            sessionId,
            windows,
            create: static () => new FakeWindow(),
            initialize: _ => initialized++,
            show: static _ => throw new InvalidOperationException("show failed"),
            cleanup: _ => cleaned++,
            rollback: () => rolledBack++);

        Assert.False(shown);
        Assert.Empty(windows);
        Assert.Equal(1, initialized);
        Assert.Equal(1, cleaned);
        Assert.Equal(1, rolledBack);
    }

    [Fact]
    public void TryShow_RollsBackWhenWindowCreationFails()
    {
        Guid sessionId = Guid.NewGuid();
        var windows = new Dictionary<Guid, FakeWindow>();
        int initialized = 0;
        int cleaned = 0;
        int rolledBack = 0;

        bool shown = DetachedWindowCoordinator.TryShow(
            sessionId,
            windows,
            create: static () => throw new InvalidOperationException("creation failed"),
            initialize: _ => initialized++,
            show: static _ => { },
            cleanup: _ => cleaned++,
            rollback: () => rolledBack++);

        Assert.False(shown);
        Assert.Empty(windows);
        Assert.Equal(0, initialized);
        Assert.Equal(0, cleaned);
        Assert.Equal(1, rolledBack);
    }

    [Fact]
    public void TryShow_RegistersExactlyOneWindowWhenSuccessful()
    {
        Guid sessionId = Guid.NewGuid();
        var windows = new Dictionary<Guid, FakeWindow>();
        int rolledBack = 0;

        bool shown = DetachedWindowCoordinator.TryShow(
            sessionId,
            windows,
            create: static () => new FakeWindow(),
            initialize: static _ => { },
            show: static window => window.WasShown = true,
            cleanup: static _ => { },
            rollback: () => rolledBack++);

        FakeWindow window = Assert.Single(windows).Value;
        Assert.True(shown);
        Assert.True(window.WasShown);
        Assert.Equal(0, rolledBack);
    }

    [Fact]
    public void CloseAll_AttemptsEveryWindowAndAlwaysClearsRegistration()
    {
        var windows = new Dictionary<Guid, FakeWindow>
        {
            [Guid.NewGuid()] = new FakeWindow { ThrowOnClose = true },
            [Guid.NewGuid()] = new FakeWindow(),
        };
        int closeAttempts = 0;

        DetachedWindowCoordinator.CloseAll(
            windows,
            window =>
            {
                closeAttempts++;
                if (window.ThrowOnClose)
                {
                    throw new InvalidOperationException("close failed");
                }

                window.WasClosed = true;
            });

        Assert.Equal(2, closeAttempts);
        Assert.Empty(windows);
    }

    private sealed class FakeWindow
    {
        public bool ThrowOnClose { get; init; }

        public bool WasClosed { get; set; }

        public bool WasShown { get; set; }
    }
}

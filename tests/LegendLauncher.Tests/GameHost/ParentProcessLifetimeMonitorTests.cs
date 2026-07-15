using LegendLauncher.GameHost.Legacy;

namespace LegendLauncher.Tests.GameHost;

public sealed class ParentProcessLifetimeMonitorTests
{
    [Fact]
    public void ExitNotification_IsDeliveredOnlyOnce()
    {
        var exitSource = new TestParentProcessExitSource();
        int notificationCount = 0;
        using var monitor = CreateMonitor(exitSource, () => notificationCount++);

        exitSource.RaiseExited();
        exitSource.RaiseExited();

        Assert.Equal(1, notificationCount);
        Assert.True(exitSource.StartedWatching);
    }

    [Fact]
    public void AlreadyExitedParent_IsDetectedDuringConstruction()
    {
        var exitSource = new TestParentProcessExitSource
        {
            HasExited = true,
        };
        int notificationCount = 0;

        using var monitor = CreateMonitor(exitSource, () => notificationCount++);

        Assert.Equal(1, notificationCount);
    }

    [Fact]
    public void ExitRaisedWhileWatchingStarts_RemainsIdempotent()
    {
        var exitSource = new TestParentProcessExitSource
        {
            RaiseExitWhenWatchingStarts = true,
            HasExited = true,
        };
        int notificationCount = 0;

        using var monitor = CreateMonitor(exitSource, () => notificationCount++);

        Assert.Equal(1, notificationCount);
    }

    [Fact]
    public void Dispose_UnsubscribesAndSuppressesLaterNotification()
    {
        var exitSource = new TestParentProcessExitSource();
        int notificationCount = 0;
        var monitor = CreateMonitor(exitSource, () => notificationCount++);

        monitor.Dispose();
        monitor.Dispose();
        exitSource.RaiseExited();

        Assert.Equal(0, notificationCount);
        Assert.Equal(1, exitSource.DisposeCount);
        Assert.Equal(0, exitSource.SubscriberCount);
    }

    [Fact]
    public void UnavailableParent_IsTreatedAsAlreadyExited()
    {
        int notificationCount = 0;

        using var monitor = new ParentProcessLifetimeMonitor(
            42,
            () => notificationCount++,
            static _ => throw new ArgumentException("Process is no longer available."));

        Assert.Equal(1, notificationCount);
    }

    private static ParentProcessLifetimeMonitor CreateMonitor(
        TestParentProcessExitSource exitSource,
        Action parentExited) =>
        new(42, parentExited, _ => exitSource);

    private sealed class TestParentProcessExitSource : IParentProcessExitSource
    {
        private EventHandler? _exited;

        public event EventHandler? Exited
        {
            add
            {
                _exited += value;
                SubscriberCount++;
            }
            remove
            {
                _exited -= value;
                SubscriberCount--;
            }
        }

        public bool HasExited { get; init; }

        public bool RaiseExitWhenWatchingStarts { get; init; }

        public bool StartedWatching { get; private set; }

        public int SubscriberCount { get; private set; }

        public int DisposeCount { get; private set; }

        public void StartWatching()
        {
            StartedWatching = true;
            if (RaiseExitWhenWatchingStarts)
            {
                RaiseExited();
            }
        }

        public void RaiseExited()
        {
            _exited?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            DisposeCount++;
        }
    }
}

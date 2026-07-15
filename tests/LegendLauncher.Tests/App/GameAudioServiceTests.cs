using LegendLauncher.App.Services;

namespace LegendLauncher.Tests.App;

public sealed class GameAudioServiceTests
{
    [Fact]
    public void RegisteredProcessesReceiveGlobalMuteChanges()
    {
        using var mutedSignal = new ManualResetEventSlim();
        using var unmutedSignal = new ManualResetEventSlim();
        using var service = new GameAudioService(
            (processIds, isMuted) =>
            {
                if (!processIds.Contains(4123))
                {
                    return;
                }

                if (isMuted)
                {
                    mutedSignal.Set();
                }
                else
                {
                    unmutedSignal.Set();
                }
            },
            TimeSpan.FromHours(1));

        service.RegisterProcess(4123);
        Assert.True(mutedSignal.Wait(TimeSpan.FromSeconds(2)));

        service.SetMuted(false);
        Assert.True(unmutedSignal.Wait(TimeSpan.FromSeconds(2)));
        Assert.False(service.IsMuted);
    }

    [Fact]
    public void UnregisteredProcessIsRemovedFromManualRefresh()
    {
        using var removedSignal = new ManualResetEventSlim();
        IReadOnlySet<int>? lastSnapshot = null;
        using var service = new GameAudioService(
            (processIds, _) =>
            {
                lastSnapshot = new HashSet<int>(processIds);
                if (processIds.Contains(5002) && !processIds.Contains(5001))
                {
                    removedSignal.Set();
                }
            },
            TimeSpan.FromHours(1));

        service.RegisterProcess(5001);
        service.RegisterProcess(5002);
        service.UnregisterProcess(5001);
        service.SetMuted(false);

        Assert.True(removedSignal.Wait(TimeSpan.FromSeconds(2)));
        Assert.NotNull(lastSnapshot);
        Assert.DoesNotContain(5001, lastSnapshot!);
        Assert.Contains(5002, lastSnapshot!);
    }

    [Fact]
    public void RecoverableCoreAudioFailureDoesNotEscapeTheServiceBoundary()
    {
        using var attempted = new ManualResetEventSlim();
        using var service = new GameAudioService(
            (_, _) =>
            {
                attempted.Set();
                throw new InvalidOperationException("Audio session is not ready yet.");
            },
            TimeSpan.FromHours(1));

        service.RegisterProcess(6001);
        Assert.True(attempted.Wait(TimeSpan.FromSeconds(2)));

        Exception? exception = Record.Exception(() => service.SetMuted(false));
        Assert.Null(exception);
    }
}

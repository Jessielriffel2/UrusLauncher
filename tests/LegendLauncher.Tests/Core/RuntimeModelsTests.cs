using LegendLauncher.Core.Models;

namespace LegendLauncher.Tests.Core;

public sealed class RuntimeModelsTests
{
    [Fact]
    public void RuntimeOptions_DefaultToSafeRenderingWithoutExposingPath()
    {
        string root = Path.Combine(Path.GetTempPath(), "private-runtime-location");
        var options = new GameRuntimeOptions(root);

        Assert.Equal(Path.GetFullPath(root), options.RuntimeRoot);
        Assert.Equal(GameRenderQuality.High, options.Quality);
        Assert.Equal(GameWindowMode.Opaque, options.WindowMode);
        Assert.DoesNotContain(root, options.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GameSession_CarriesValidatedNativeSurfaceIdentity()
    {
        var startedAt = DateTimeOffset.UnixEpoch;
        nint windowHandle = new(0x123456);

        var session = new GameSession(42, windowHandle, startedAt);

        Assert.Equal(42, session.ProcessId);
        Assert.Equal(windowHandle, session.NativeWindowHandle);
        Assert.Equal(startedAt, session.StartedAtUtc);
    }
}

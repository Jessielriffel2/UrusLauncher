using LegendLauncher.Core.Models;

namespace LegendLauncher.Tests.Core;

public sealed class RuntimeModelsTests
{
    [Theory]
    [InlineData(GameWindowMode.Direct)]
    [InlineData(GameWindowMode.Opaque)]
    public void RuntimeOptions_KeepTheChosenWindowModeWithoutExposingPath(GameWindowMode windowMode)
    {
        string root = Path.Combine(Path.GetTempPath(), "private-runtime-location");
        var options = new GameRuntimeOptions(root, windowMode);

        Assert.Equal(Path.GetFullPath(root), options.RuntimeRoot);
        Assert.Equal(GameRenderQuality.High, options.Quality);
        Assert.Equal(windowMode, options.WindowMode);
        Assert.DoesNotContain(root, options.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RuntimeOptions_RejectAnUndefinedWindowMode()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GameRuntimeOptions(Path.GetTempPath(), (GameWindowMode)42));
    }

    [Theory]
    [InlineData(GameWindowMode.Opaque, "opaque")]
    [InlineData(GameWindowMode.Direct, "direct")]
    public void WindowModeText_IsFlashsOwnWModeValueBothWays(GameWindowMode windowMode, string text)
    {
        Assert.Equal(text, GameWindowModeText.Format(windowMode));
        Assert.True(GameWindowModeText.TryParse(text, out GameWindowMode parsed));
        Assert.Equal(windowMode, parsed);
        Assert.True(GameWindowModeText.TryParse(text.ToUpperInvariant(), out parsed));
        Assert.Equal(windowMode, parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("gpu")]
    [InlineData("window")]
    [InlineData(" direct")]
    public void WindowModeText_RejectsAnythingElse(string? text)
    {
        Assert.False(GameWindowModeText.TryParse(text, out _));
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

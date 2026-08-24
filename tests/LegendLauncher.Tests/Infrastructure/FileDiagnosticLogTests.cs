using LegendLauncher.Infrastructure.Logging;
using LegendLauncher.Infrastructure.Paths;

namespace LegendLauncher.Tests.Infrastructure;

public sealed class FileDiagnosticLogTests
{
    [Fact]
    public void WriteFailure_CreatesDailyLogWithTimestampAndException()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 8, 23, 21, 52, 11, 456, TimeSpan.FromHours(-3)));
        var log = new FileDiagnosticLog(
            temporaryDirectory.Combine("uruslauncher", "logs"),
            time,
            launcherVersion: "1.1.8");
        var exception = new InvalidOperationException("catalog timeout");

        log.WriteFailure(
            "catalog.load",
            "The server catalog could not be loaded.",
            exception,
            [
                new("platformId", "oas-lorpt"),
                new("serverId", "100"),
            ]);

        string path = Path.Combine(log.LogsDirectory, "uruslauncher-20260823.log");
        Assert.True(File.Exists(path));
        string content = File.ReadAllText(path);
        Assert.Contains("2026-08-23 21:52:11.456 -03:00", content, StringComparison.Ordinal);
        Assert.Contains("Operation: catalog.load", content, StringComparison.Ordinal);
        Assert.Contains("platformId: oas-lorpt", content, StringComparison.Ordinal);
        Assert.Contains("serverId: 100", content, StringComparison.Ordinal);
        Assert.Contains("InvalidOperationException", content, StringComparison.Ordinal);
        Assert.Contains("catalog timeout", content, StringComparison.Ordinal);
        Assert.Contains("LauncherVersion: 1.1.8", content, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteFailure_RedactsPasswordMaterial()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var log = new FileDiagnosticLog(temporaryDirectory.Combine("logs"), launcherVersion: "1.1.8");
        var exception = new InvalidOperationException("password=super-secret token=abc123");

        log.WriteFailure(
            "session.authentication_rejected",
            "Authentication was rejected.",
            exception,
            [new("login", "player@example.test")]);

        string content = File.ReadAllText(
            Directory.GetFiles(log.LogsDirectory, "uruslauncher-*.log").Single());
        Assert.DoesNotContain("super-secret", content, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", content, StringComparison.Ordinal);
        Assert.Contains("password=***", content, StringComparison.Ordinal);
        Assert.Contains("player@example.test", content, StringComparison.Ordinal);
    }

    [Fact]
    public void AppPaths_PlacesLogsUnderDocumentsUrusLauncher()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = new AppPaths(
            temporaryDirectory.Combine("local"),
            temporaryDirectory.Combine("documents"),
            AppPaths.DefaultApplicationDirectoryName);

        Assert.Equal(
            Path.GetFullPath(Path.Combine(temporaryDirectory.Combine("documents"), "uruslauncher", "logs")),
            paths.LogsDirectory);
        Assert.False(
            paths.LogsDirectory.StartsWith(paths.RootDirectory, StringComparison.OrdinalIgnoreCase));
        paths.EnsureDirectories();
        Assert.False(Directory.Exists(paths.LogsDirectory));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();

        public override TimeZoneInfo LocalTimeZone { get; } = TimeZoneInfo.CreateCustomTimeZone(
            "test-log",
            now.Offset,
            "Test",
            "Test");
    }
}

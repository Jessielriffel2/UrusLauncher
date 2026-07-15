using LegendLauncher.App.Updates;
using System.Net.Http;

namespace LegendLauncher.Tests.App.Updates;

public sealed class UpdateDownloadCleanupTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ServiceStartupDeletesOnlyStaleOfficialArtifactsInExactDirectory()
    {
        using var directory = new TemporaryUpdateDirectory();
        string staleInstaller = CreateArtifact(
            directory.Path,
            "UrusLauncher-Setup-1.0.0-win-x64.exe",
            Now.AddHours(-25));
        string stalePartial = CreateArtifact(
            directory.Path,
            "UrusLauncher-Setup-1.1.0-win-x64.exe.part",
            Now.AddDays(-2));
        string recentInstaller = CreateArtifact(
            directory.Path,
            "UrusLauncher-Setup-1.2.0-win-x64.exe",
            Now.AddHours(-23));
        string exactlyTwentyFourHours = CreateArtifact(
            directory.Path,
            "UrusLauncher-Setup-1.2.1-win-x64.exe.part",
            Now.AddHours(-24));
        string unrelatedPartial = CreateArtifact(
            directory.Path,
            "notes.part",
            Now.AddDays(-10));
        string lookalikeInstaller = CreateArtifact(
            directory.Path,
            "UrusLauncher-Setup-1.2-win-x64.exe",
            Now.AddDays(-10));
        string backupFile = CreateArtifact(
            directory.Path,
            "UrusLauncher-Setup-1.2.3-win-x64.exe.backup",
            Now.AddDays(-10));
        string nestedDirectory = Path.Combine(directory.Path, "nested");
        Directory.CreateDirectory(nestedDirectory);
        string nestedInstaller = CreateArtifact(
            nestedDirectory,
            "UrusLauncher-Setup-0.9.0-win-x64.exe",
            Now.AddDays(-10));

        using var handler = new QueueHttpMessageHandler();
        _ = CreateService(handler, directory);

        Assert.False(File.Exists(staleInstaller));
        Assert.False(File.Exists(stalePartial));
        Assert.True(File.Exists(recentInstaller));
        Assert.True(File.Exists(exactlyTwentyFourHours));
        Assert.True(File.Exists(unrelatedPartial));
        Assert.True(File.Exists(lookalikeInstaller));
        Assert.True(File.Exists(backupFile));
        Assert.True(File.Exists(nestedInstaller));
        Assert.Empty(handler.RequestedUris);
    }

    [Fact]
    public void ServiceStartupIgnoresLockedOfficialArtifact()
    {
        using var directory = new TemporaryUpdateDirectory();
        string installerPath = CreateArtifact(
            directory.Path,
            "UrusLauncher-Setup-1.0.0-win-x64.exe",
            Now.AddDays(-2));
        using var lockStream = new FileStream(
            installerPath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);
        using var handler = new QueueHttpMessageHandler();

        LauncherUpdateService service = CreateService(handler, directory);

        Assert.NotNull(service);
        Assert.True(File.Exists(installerPath));
        Assert.Empty(handler.RequestedUris);
    }

    [Fact]
    public void ServiceStartupIgnoresInaccessibleOfficialArtifact()
    {
        using var directory = new TemporaryUpdateDirectory();
        string installerPath = CreateArtifact(
            directory.Path,
            "UrusLauncher-Setup-1.0.0-win-x64.exe.part",
            Now.AddDays(-2));
        File.SetAttributes(installerPath, FileAttributes.ReadOnly);
        try
        {
            using var handler = new QueueHttpMessageHandler();

            LauncherUpdateService service = CreateService(handler, directory);

            Assert.NotNull(service);
            Assert.True(File.Exists(installerPath));
            Assert.Empty(handler.RequestedUris);
        }
        finally
        {
            if (File.Exists(installerPath))
            {
                File.SetAttributes(installerPath, FileAttributes.Normal);
            }
        }
    }

    [Theory]
    [InlineData("UrusLauncher-Setup-0.0.1-win-x64.exe", true)]
    [InlineData("UrusLauncher-Setup-10.20.30-win-x64.exe.part", true)]
    [InlineData("UrusLauncher-Setup-01.2.3-win-x64.exe", false)]
    [InlineData("urusLauncher-Setup-1.2.3-win-x64.exe", false)]
    [InlineData("UrusLauncher-Setup-1.2.3-win-x86.exe", false)]
    [InlineData("UrusLauncher-Setup-1.2.3-win-x64.exe.tmp", false)]
    [InlineData("random.part", false)]
    public void OfficialArtifactRecognitionIsExact(string fileName, bool expected)
    {
        Assert.Equal(expected, UpdateDownloadCleanup.IsOfficialArtifact(fileName));
    }

    private static LauncherUpdateService CreateService(
        QueueHttpMessageHandler handler,
        TemporaryUpdateDirectory directory) =>
        new(
            new HttpClient(handler),
            directory.Path,
            timeProvider: new FixedTimeProvider(Now));

    private static string CreateArtifact(
        string directory,
        string fileName,
        DateTimeOffset lastWriteUtc)
    {
        string path = Path.Combine(directory, fileName);
        File.WriteAllText(path, "artifact");
        File.SetLastWriteTimeUtc(path, lastWriteUtc.UtcDateTime);
        return path;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}

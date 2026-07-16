using LegendLauncher.App;

namespace LegendLauncher.Tests.App;

public sealed class LauncherCompositionTests
{
    [Fact]
    public void RuntimeDiscovery_PrefersTheRuntimeBundledWithTheApplication()
    {
        using var temporaryDirectory = new Infrastructure.TemporaryDirectory();
        string applicationDirectory = temporaryDirectory.Combine("app");
        string bundledRuntime = Path.Combine(applicationDirectory, "runtime");
        string configuredRuntime = temporaryDirectory.Combine("configured");
        string programFiles = temporaryDirectory.Combine("program-files");
        string knownInstallation = Path.Combine(
            programFiles,
            "Legend Online Client by Brov (H2_x64)");
        Directory.CreateDirectory(bundledRuntime);
        Directory.CreateDirectory(configuredRuntime);
        Directory.CreateDirectory(knownInstallation);

        string? result = LauncherComposition.FindLegacyRuntimeCandidate(
            applicationDirectory,
            configuredRuntime,
            [programFiles]);

        Assert.Equal(bundledRuntime, result);
    }

    [Fact]
    public void RuntimeDiscovery_FallsBackToConfiguredAndKnownInstallations()
    {
        using var temporaryDirectory = new Infrastructure.TemporaryDirectory();
        string applicationDirectory = temporaryDirectory.Combine("app");
        string configuredRuntime = temporaryDirectory.Combine("configured");
        string programFiles = temporaryDirectory.Combine("program-files");
        string knownInstallation = Path.Combine(
            programFiles,
            "Legend Online Client by Brov (64-bit)");
        Directory.CreateDirectory(applicationDirectory);
        Directory.CreateDirectory(configuredRuntime);
        Directory.CreateDirectory(knownInstallation);

        Assert.Equal(
            configuredRuntime,
            LauncherComposition.FindLegacyRuntimeCandidate(
                applicationDirectory,
                configuredRuntime,
                [programFiles]));

        Directory.Delete(configuredRuntime);

        Assert.Equal(
            knownInstallation,
            LauncherComposition.FindLegacyRuntimeCandidate(
                applicationDirectory,
                configuredRuntime,
                [programFiles]));
    }
}

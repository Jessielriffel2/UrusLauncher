using LegendLauncher.Infrastructure.Runtime;

namespace LegendLauncher.Tests.Infrastructure;

public sealed class LegacyRuntimeProbeTests
{
    [Fact]
    public void Probe_FindsCompleteRuntimeByWalkingParentDirectories()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var runtime = temporaryDirectory.Combine("legacy");
        var start = Path.Combine(runtime, "app", "bin");
        CreateRuntime(runtime);
        Directory.CreateDirectory(start);
        var filesBeforeProbe = Directory.EnumerateFiles(runtime, "*", SearchOption.AllDirectories).ToArray();

        var result = new LegacyRuntimeProbe().Probe(startDirectory: start);

        Assert.True(result.IsUsable);
        Assert.Equal(runtime, result.RuntimeDirectory);
        Assert.Equal(LegacyRuntimeProbeSource.AncestorSearch, result.Source);
        Assert.Empty(result.MissingComponents);
        Assert.Equal(
            filesBeforeProbe,
            Directory.EnumerateFiles(runtime, "*", SearchOption.AllDirectories).ToArray());
    }

    [Fact]
    public void Probe_PrefersValidConfiguredPath()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var runtime = temporaryDirectory.Combine("configured-runtime");
        CreateRuntime(runtime);

        var result = new LegacyRuntimeProbe().Probe(
            configuredPath: runtime,
            startDirectory: temporaryDirectory.Combine("unrelated"));

        Assert.True(result.IsUsable);
        Assert.Equal(runtime, result.RuntimeDirectory);
        Assert.Equal(LegacyRuntimeProbeSource.ConfiguredPath, result.Source);
        Assert.Equal(Path.Combine(runtime, "Adobe.Flash.Control.manifest"), result.ManifestPath);
        Assert.Equal(Path.Combine(runtime, "flash", "Flash64_test.ocx"), result.FlashOcxPath);
    }

    [Fact]
    public void Probe_ReportsMissingComponentsForPartialRuntime()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        File.WriteAllText(
            temporaryDirectory.Combine("Adobe.Flash.Control.manifest"),
            "<assembly xmlns=\"urn:schemas-microsoft-com:asm.v1\" manifestVersion=\"1.0\" />");

        var result = new LegacyRuntimeProbe().Probe(configuredPath: temporaryDirectory.Path);

        Assert.False(result.IsUsable);
        Assert.Equal(temporaryDirectory.Path, result.RuntimeDirectory);
        Assert.DoesNotContain(LegacyRuntimeComponent.FlashManifest, result.MissingComponents);
        Assert.Contains(LegacyRuntimeComponent.FlashActiveXControl, result.MissingComponents);
    }

    [Fact]
    public void Probe_RejectsManifestOcxOutsideRuntimeDirectory()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var runtime = temporaryDirectory.Combine("legacy");
        Directory.CreateDirectory(runtime);
        File.WriteAllText(temporaryDirectory.Combine("outside.ocx"), "not-owned-by-runtime");
        File.WriteAllText(
            Path.Combine(runtime, "Adobe.Flash.Control.manifest"),
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <assembly xmlns="urn:schemas-microsoft-com:asm.v1" manifestVersion="1.0">
              <file name="..\outside.ocx" />
            </assembly>
            """);

        var result = new LegacyRuntimeProbe().Probe(configuredPath: runtime);

        Assert.False(result.IsUsable);
        Assert.Null(result.FlashOcxPath);
        Assert.Contains(LegacyRuntimeComponent.FlashActiveXControl, result.MissingComponents);
    }

    private static void CreateRuntime(string directory)
    {
        Directory.CreateDirectory(Path.Combine(directory, "flash"));
        File.WriteAllText(
            Path.Combine(directory, "Adobe.Flash.Control.manifest"),
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <assembly xmlns="urn:schemas-microsoft-com:asm.v1" manifestVersion="1.0">
              <file name="flash\Flash64_test.ocx" />
            </assembly>
            """);
        File.WriteAllText(Path.Combine(directory, "flash", "Flash64_test.ocx"), "ocx");
    }
}

namespace LegendLauncher.Infrastructure.Runtime;

public enum LegacyRuntimeProbeSource
{
    NotFound,
    ConfiguredPath,
    AncestorSearch
}

public enum LegacyRuntimeComponent
{
    FlashManifest,
    FlashActiveXControl
}

public sealed record LegacyRuntimeProbeResult(
    bool IsUsable,
    string? RuntimeDirectory,
    string? ManifestPath,
    string? FlashOcxPath,
    IReadOnlyList<LegacyRuntimeComponent> MissingComponents,
    LegacyRuntimeProbeSource Source);

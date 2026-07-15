using System.Text.Json.Serialization;

namespace LegendLauncher.App.Updates;

internal sealed record GitHubReleaseDocument(
    [property: JsonPropertyName("tag_name")] string? TagName,
    [property: JsonPropertyName("assets")] GitHubReleaseAssetDocument[]? Assets);

internal sealed record GitHubReleaseAssetDocument(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("browser_download_url")] string? BrowserDownloadUrl,
    [property: JsonPropertyName("size")] long? Size,
    [property: JsonPropertyName("digest")] string? Digest);

internal sealed record UpdateManifestDocument(
    [property: JsonPropertyName("schema")] int Schema,
    [property: JsonPropertyName("repository")] string? Repository,
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("installer")] UpdateManifestInstallerDocument? Installer,
    [property: JsonPropertyName("notes")] Dictionary<string, string?>? Notes);

internal sealed record UpdateManifestInstallerDocument(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("bytes")] long Bytes,
    [property: JsonPropertyName("sha256")] string? Sha256);

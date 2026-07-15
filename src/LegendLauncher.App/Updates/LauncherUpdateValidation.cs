using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace LegendLauncher.App.Updates;

internal static partial class LauncherUpdateValidation
{
    public const string Repository = "Jessielriffel2/UrusLauncher";
    public const string ManifestAssetName = "update-manifest.json";
    public const long MaximumInstallerBytes = 250L * 1024 * 1024;
    public const long MaximumJsonBytes = 2L * 1024 * 1024;

    public static Uri LatestReleaseUri { get; } =
        new($"https://api.github.com/repos/{Repository}/releases/latest");

    public static Version ParseTag(string? tagName)
    {
        Match match = ReleaseTagRegex().Match(tagName ?? string.Empty);
        if (!match.Success ||
            !int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int major) ||
            !int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int minor) ||
            !int.TryParse(match.Groups[3].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int build))
        {
            throw new InvalidDataException("The GitHub release tag must use the vX.Y.Z format.");
        }

        return new Version(major, minor, build);
    }

    public static Version ParseManifestVersion(string? value)
    {
        Match match = ManifestVersionRegex().Match(value ?? string.Empty);
        if (!match.Success ||
            !int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int major) ||
            !int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int minor) ||
            !int.TryParse(match.Groups[3].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int build))
        {
            throw new InvalidDataException("The update manifest version must use the X.Y.Z format.");
        }

        return new Version(major, minor, build);
    }

    public static Version NormalizeCurrentVersion(Version version)
    {
        ArgumentNullException.ThrowIfNull(version);
        if (version.Major < 0 || version.Minor < 0)
        {
            throw new ArgumentException("The current launcher version is invalid.", nameof(version));
        }

        return new Version(version.Major, version.Minor, Math.Max(version.Build, 0));
    }

    public static string ExpectedInstallerName(Version version) =>
        $"UrusLauncher-Setup-{version.ToString(3)}-win-x64.exe";

    public static bool IsInstallerName(string? value) =>
        InstallerNameRegex().IsMatch(value ?? string.Empty);

    public static void ValidateSha256(string? sha256, string fieldName)
    {
        if (!Sha256Regex().IsMatch(sha256 ?? string.Empty))
        {
            throw new InvalidDataException($"{fieldName} must be a 64-character SHA-256 value.");
        }
    }

    public static string? ParseGitHubDigest(string? digest, string assetName)
    {
        if (string.IsNullOrWhiteSpace(digest))
        {
            return null;
        }

        const string prefix = "sha256:";
        if (!digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"The GitHub digest for {assetName} is not SHA-256.");
        }

        string value = digest[prefix.Length..];
        ValidateSha256(value, $"The GitHub digest for {assetName}");
        return value;
    }

    public static Uri ParseReleaseAssetUri(
        string? value,
        string tagName,
        string assetName)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
        {
            throw new InvalidDataException($"The GitHub URL for {assetName} is invalid.");
        }

        ValidateAllowedUri(uri);
        if (!uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"The GitHub URL for {assetName} must use github.com.");
        }

        string expectedPath = $"/{Repository}/releases/download/{tagName}/{assetName}";
        if (!uri.AbsolutePath.Equals(expectedPath, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"The GitHub URL for {assetName} does not belong to the expected release.");
        }

        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidDataException($"The GitHub URL for {assetName} contains unexpected data.");
        }

        return uri;
    }

    public static void ValidateAllowedUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri ||
            !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !uri.IsDefaultPort)
        {
            throw new InvalidDataException("The update URL must be a standard HTTPS GitHub URL.");
        }

        string host = uri.IdnHost;
        if (host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase))
        {
            string prefix = $"/repos/{Repository}/";
            if (!uri.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The GitHub API URL does not belong to the update repository.");
            }

            return;
        }

        if (host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
        {
            string prefix = $"/{Repository}/";
            if (!uri.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The GitHub URL does not belong to the update repository.");
            }

            return;
        }

        if (host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new InvalidDataException("The update URL host is not allowed.");
    }

    public static bool FixedTimeSha256Equals(string expected, string actual) =>
        System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(expected),
            Convert.FromHexString(actual));

    [GeneratedRegex(@"^v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$", RegexOptions.CultureInvariant)]
    private static partial Regex ReleaseTagRegex();

    [GeneratedRegex(@"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$", RegexOptions.CultureInvariant)]
    private static partial Regex ManifestVersionRegex();

    [GeneratedRegex("^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Regex();

    [GeneratedRegex(
        @"^UrusLauncher-Setup-(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)-win-x64\.exe$",
        RegexOptions.CultureInvariant)]
    private static partial Regex InstallerNameRegex();
}

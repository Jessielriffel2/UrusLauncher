using LegendLauncher.Core.Models;

namespace LegendLauncher.Providers.Oas;

/// <summary>
/// Restricts authentication traffic to the verified OAS HTTPS origins.
/// </summary>
internal static class OasOriginPolicy
{
    private const string OasDomain = "creaction-network.com";

    public static bool TryGetPlatformHost(
        PlatformDefinition platform,
        out string platformHost)
    {
        platformHost = string.Empty;

        var knownPlatform = OasPlatformCatalog.Find(platform.Id);
        if (knownPlatform is null ||
            !string.Equals(
                knownPlatform.GameCode,
                platform.GameCode,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        platformHost = $"{knownPlatform.GameCode.ToLowerInvariant()}.{OasDomain}";
        return true;
    }

    public static bool IsAllowedPlatformUri(Uri uri, string platformHost) =>
        IsSafeHttpsUri(uri) &&
        string.Equals(uri.IdnHost, platformHost, StringComparison.OrdinalIgnoreCase);

    public static bool IsAllowedGameUri(Uri uri) =>
        IsSafeHttpsUri(uri) &&
        (string.Equals(uri.IdnHost, OasDomain, StringComparison.OrdinalIgnoreCase) ||
         uri.IdnHost.EndsWith($".{OasDomain}", StringComparison.OrdinalIgnoreCase));

    public static bool IsPassportUri(Uri uri, Uri expectedEndpoint) =>
        IsSafeHttpsUri(uri) &&
        string.Equals(
            uri.IdnHost,
            expectedEndpoint.IdnHost,
            StringComparison.OrdinalIgnoreCase) &&
        string.Equals(
            uri.AbsolutePath,
            expectedEndpoint.AbsolutePath,
            StringComparison.Ordinal);

    private static bool IsSafeHttpsUri(Uri uri) =>
        uri.IsAbsoluteUri &&
        string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
        uri.Port == 443 &&
        string.IsNullOrEmpty(uri.UserInfo) &&
        !string.IsNullOrWhiteSpace(uri.IdnHost);
}

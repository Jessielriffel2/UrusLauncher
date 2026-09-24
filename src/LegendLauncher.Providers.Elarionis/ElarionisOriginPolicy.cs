using LegendLauncher.Core.Models;

namespace LegendLauncher.Providers.Elarionis;

/// <summary>
/// Restricts Elarionis authentication traffic to the verified HTTPS origin.
/// </summary>
internal static class ElarionisOriginPolicy
{
    public const string AllowedHost = "elarionis.online";

    public static bool IsAllowedHost(string host) =>
        string.Equals(host.TrimEnd('.'), AllowedHost, StringComparison.OrdinalIgnoreCase);

    public static bool IsSafeHttpsUri(Uri uri) =>
        uri.IsAbsoluteUri &&
        string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
        uri.Port == 443 &&
        string.IsNullOrEmpty(uri.UserInfo) &&
        !string.IsNullOrWhiteSpace(uri.IdnHost);

    public static bool IsAllowedApiUri(Uri uri) =>
        IsSafeHttpsUri(uri) && IsAllowedHost(uri.IdnHost);

    public static bool IsAllowedGameUri(Uri uri) =>
        IsSafeHttpsUri(uri) && IsAllowedHost(uri.IdnHost);

    /// <summary>
    /// Validates the token-less play document address produced by the server directory,
    /// e.g. https://elarionis.online/s7/play?site=s7 or https://elarionis.online/play?site=local.
    /// </summary>
    public static bool IsPlayDocumentUri(Uri uri)
    {
        if (!IsAllowedGameUri(uri))
        {
            return false;
        }

        string path = uri.AbsolutePath;
        if (!path.EndsWith("/play", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(GetQueryParameter(uri, "site"));
    }

    public static bool TryGetPlatformHost(PlatformDefinition platform, out string platformHost)
    {
        platformHost = string.Empty;
        var known = ElarionisPlatformCatalog.Find(platform.Id);
        if (known is null ||
            !string.Equals(platform.GameCode, known.GameCode, StringComparison.OrdinalIgnoreCase) ||
            !Uri.Equals(platform.ServerListEndpoint, known.ServerListEndpoint))
        {
            return false;
        }

        platformHost = AllowedHost;
        return true;
    }

    internal static string? GetQueryParameter(Uri uri, string expectedName)
    {
        var query = uri.Query.TrimStart('?');
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            var encodedName = separator >= 0 ? part[..separator] : part;
            if (!string.Equals(
                    DecodeQueryComponent(encodedName),
                    expectedName,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var encodedValue = separator >= 0 ? part[(separator + 1)..] : string.Empty;
            return DecodeQueryComponent(encodedValue);
        }

        return null;
    }

    private static string DecodeQueryComponent(string value)
    {
        try
        {
            return Uri.UnescapeDataString(value.Replace('+', ' '));
        }
        catch (UriFormatException)
        {
            return string.Empty;
        }
    }
}

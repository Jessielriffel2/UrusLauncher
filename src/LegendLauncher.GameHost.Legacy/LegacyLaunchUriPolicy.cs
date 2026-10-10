using LegendLauncher.NetworkBridge;

namespace LegendLauncher.GameHost.Legacy;

internal static class LegacyLaunchUriPolicy
{
    private const int MaxUriLength = 8 * 1024;
    private static readonly BridgeSecurityPolicy BridgePolicy = new();

    public static bool IsAllowed(Uri? launchUri)
    {
        if (launchUri is null ||
            !launchUri.IsAbsoluteUri ||
            launchUri.AbsoluteUri.Length > MaxUriLength ||
            !string.IsNullOrEmpty(launchUri.UserInfo))
        {
            return false;
        }

#if LOREBORN_LOCAL
        // The local provider serves the game from the user's own machine over
        // plain HTTP on loopback; remote providers stay HTTPS-only on 443.
        if (IsLoopback(launchUri))
        {
            return string.Equals(launchUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
        }
#endif

        if (!string.Equals(launchUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            (!launchUri.IsDefaultPort && launchUri.Port != 443))
        {
            return false;
        }

        return BridgePolicy.ValidateUpstream(launchUri).IsAllowed;
    }

#if LOREBORN_LOCAL
    private static bool IsLoopback(Uri uri) =>
        uri.IsLoopback ||
        string.Equals(uri.IdnHost.TrimEnd('.'), "localhost", StringComparison.OrdinalIgnoreCase);

#endif
    public static void EnsureAllowed(Uri? launchUri)
    {
        if (!IsAllowed(launchUri))
        {
            throw new UnauthorizedAccessException(
                "The game address is outside the HTTPS launch allowlist.");
        }
    }
}

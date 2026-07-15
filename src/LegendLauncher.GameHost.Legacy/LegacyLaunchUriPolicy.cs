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
            !string.Equals(launchUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            (!launchUri.IsDefaultPort && launchUri.Port != 443))
        {
            return false;
        }

        return BridgePolicy.ValidateUpstream(launchUri).IsAllowed;
    }

    public static void EnsureAllowed(Uri? launchUri)
    {
        if (!IsAllowed(launchUri))
        {
            throw new UnauthorizedAccessException(
                "The game address is outside the HTTPS launch allowlist.");
        }
    }
}

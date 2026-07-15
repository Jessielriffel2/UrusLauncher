using System.Net;

namespace LegendLauncher.NetworkBridge;

/// <summary>
/// Limits the future local compatibility bridge to loopback and known game domains.
/// </summary>
public sealed class BridgeSecurityPolicy
{
    private static readonly string[] DefaultHostSuffixes =
    [
        "oasgames.com",
        "creaction-network.com",
    ];

    private readonly HashSet<string> _allowedHostSuffixes;

    public BridgeSecurityPolicy(IEnumerable<string>? allowedHostSuffixes = null)
    {
        _allowedHostSuffixes = new HashSet<string>(
            (allowedHostSuffixes ?? DefaultHostSuffixes).Select(NormalizeSuffix),
            StringComparer.OrdinalIgnoreCase);

        if (_allowedHostSuffixes.Count == 0)
        {
            throw new ArgumentException("At least one upstream host suffix is required.", nameof(allowedHostSuffixes));
        }
    }

    public IReadOnlySet<string> AllowedHostSuffixes => _allowedHostSuffixes;

    public void EnsureLoopback(IPEndPoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!IPAddress.IsLoopback(endpoint.Address))
        {
            throw new InvalidOperationException("The network bridge can listen only on loopback.");
        }
    }

    public BridgeValidationResult ValidateUpstream(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!uri.IsAbsoluteUri)
        {
            return BridgeValidationResult.Deny("Only absolute upstream addresses are accepted.");
        }

        if (uri.Scheme is not ("https" or "wss"))
        {
            return BridgeValidationResult.Deny("The upstream connection must use HTTPS or WSS.");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return BridgeValidationResult.Deny("Credentials cannot be embedded in an upstream address.");
        }

        if (IPAddress.TryParse(uri.Host, out _))
        {
            return BridgeValidationResult.Deny("Literal upstream IP addresses are not allowed.");
        }

        string host = uri.IdnHost.TrimEnd('.');
        bool isAllowed = _allowedHostSuffixes.Any(suffix =>
            host.Equals(suffix, StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith('.' + suffix, StringComparison.OrdinalIgnoreCase));

        return isAllowed
            ? BridgeValidationResult.Allow()
            : BridgeValidationResult.Deny("The upstream host is outside the game allowlist.");
    }

    private static string NormalizeSuffix(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim().TrimStart('.').TrimEnd('.').ToLowerInvariant();
    }
}

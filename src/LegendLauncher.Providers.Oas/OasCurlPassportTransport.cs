using System.Diagnostics;
using System.Net;

namespace LegendLauncher.Providers.Oas;

internal interface IOasPassportTransport
{
    Task<HttpResponseMessage> SendGetAsync(
        Uri requestUri,
        CancellationToken cancellationToken);
}

/// <summary>
/// Uses the Windows inbox curl for the exact OAS Passport endpoints blocked by the
/// Cloudflare edge when accessed through the .NET HTTP stack. The sensitive query is
/// supplied through stdin and is never a process argument.
/// </summary>
internal sealed class OasCurlPassportTransport : IOasPassportTransport
{
    internal static readonly Uri Endpoint =
        new("https://passport.creaction-network.com/index.php", UriKind.Absolute);

    internal static readonly Uri OasGamesEndpoint =
        new("https://passport.oasgames.com/index.php", UriKind.Absolute);

    private static readonly IReadOnlyList<Uri> AllowedEndpoints =
        Array.AsReadOnly(new[] { Endpoint, OasGamesEndpoint });

    private const string AcceptMediaType = "application/json";
    private readonly OasSystemCurlTransport _transport;

    public OasCurlPassportTransport(TimeSpan requestTimeout, int maximumResponseBytes) =>
        _transport = new OasSystemCurlTransport(requestTimeout, maximumResponseBytes);

    public Task<HttpResponseMessage> SendGetAsync(
        Uri requestUri,
        CancellationToken cancellationToken)
    {
        ValidateRequestUri(requestUri);
        return _transport.SendGetAsync(
            requestUri,
            new CookieContainer(),
            AcceptMediaType,
            cancellationToken);
    }

    internal ProcessStartInfo CreateProcessStartInfo() =>
        _transport.CreateProcessStartInfo();

    internal static string BuildRequestConfig(Uri requestUri)
    {
        ValidateRequestUri(requestUri);
        return OasSystemCurlTransport.BuildRequestConfig(
            requestUri,
            new CookieContainer(),
            AcceptMediaType);
    }

    private static void ValidateRequestUri(Uri requestUri)
    {
        ArgumentNullException.ThrowIfNull(requestUri);
        if (!IsAllowedEndpoint(requestUri) ||
            !string.IsNullOrEmpty(requestUri.Fragment) ||
            !HasExactLoginQuery(requestUri))
        {
            throw new ArgumentException(
                "The compatible Passport transport accepts only the exact OAS HTTPS endpoints.",
                nameof(requestUri));
        }
    }

    private static bool IsAllowedEndpoint(Uri requestUri) =>
        AllowedEndpoints.Any(endpoint => OasOriginPolicy.IsPassportUri(requestUri, endpoint));

    private static bool HasExactLoginQuery(Uri requestUri)
    {
        var parts = requestUri.Query
            .TrimStart('?')
            .Split('&', StringSplitOptions.None);
        return parts.Length == 3 &&
            string.Equals(parts[0], "m=login", StringComparison.Ordinal) &&
            parts[1].StartsWith("email=", StringComparison.Ordinal) &&
            parts[1].Length > "email=".Length &&
            parts[2].StartsWith("pwd=", StringComparison.Ordinal) &&
            parts[2].Length > "pwd=".Length;
    }
}

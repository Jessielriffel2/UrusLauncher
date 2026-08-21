using System.Diagnostics;
using System.Net;

namespace LegendLauncher.Providers.Oas;

/// <summary>
/// Uses the shared Windows curl executor only for allowlisted OAS launch pages.
/// </summary>
internal sealed class OasCurlLaunchTransport
{
    private const string AcceptMediaType = "text/html";
    private readonly OasSystemCurlTransport _transport;

    public OasCurlLaunchTransport(TimeSpan requestTimeout, int maximumResponseBytes) =>
        _transport = new OasSystemCurlTransport(requestTimeout, maximumResponseBytes);

    public Task<HttpResponseMessage> SendGetAsync(
        Uri requestUri,
        CookieContainer cookies,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requestUri);
        ArgumentNullException.ThrowIfNull(cookies);
        if (!OasOriginPolicy.IsAllowedGameUri(requestUri))
        {
            throw new ArgumentException(
                "The compatible OAS launch transport accepts only allowlisted HTTPS addresses.",
                nameof(requestUri));
        }

        return _transport.SendGetAsync(
            requestUri,
            cookies,
            AcceptMediaType,
            cancellationToken);
    }

    internal ProcessStartInfo CreateProcessStartInfo() =>
        _transport.CreateProcessStartInfo();

    internal static string BuildRequestConfig(Uri requestUri, CookieContainer cookies)
    {
        ArgumentNullException.ThrowIfNull(requestUri);
        if (!OasOriginPolicy.IsAllowedGameUri(requestUri))
        {
            throw new ArgumentException(
                "The compatible OAS launch transport accepts only allowlisted HTTPS addresses.",
                nameof(requestUri));
        }

        return OasSystemCurlTransport.BuildRequestConfig(
            requestUri,
            cookies,
            AcceptMediaType);
    }

    internal static string EscapeConfigValue(string value) =>
        OasSystemCurlTransport.EscapeConfigValue(value);
}

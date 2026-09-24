namespace LegendLauncher.Providers.Elarionis;

/// <summary>
/// Stable launcher-side error codes returned by <see cref="ElarionisAuthenticationService"/>.
/// Values intentionally match the OAS set so the existing localized
/// authentication messages apply without new resource keys.
/// </summary>
public static class ElarionisAuthenticationErrorCodes
{
    public const string InvalidCredentials = "invalid_credentials";
    public const string UnsupportedPlatform = "unsupported_platform";
    public const string InvalidServer = "invalid_server";
    public const string AuthenticationRejected = "authentication_rejected";
    public const string HttpError = "http_error";
    public const string NetworkError = "network_error";
    public const string RequestTimeout = "request_timeout";
    public const string ResponseTooLarge = "response_too_large";
    public const string InvalidAuthenticationResponse = "invalid_authentication_response";
    public const string InvalidLaunchResponse = "invalid_launch_response";
    public const string OriginNotAllowed = "origin_not_allowed";
}

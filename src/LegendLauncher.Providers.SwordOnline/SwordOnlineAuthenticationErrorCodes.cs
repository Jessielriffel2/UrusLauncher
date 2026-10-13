namespace LegendLauncher.Providers.SwordOnline;

/// <summary>
/// Stable launcher-side error codes returned by <see cref="SwordOnlineAuthenticationService"/>.
/// Values intentionally match the OAS/Elarionis set so the existing localized
/// authentication messages apply without new resource keys.
/// </summary>
public static class SwordOnlineAuthenticationErrorCodes
{
    public const string InvalidCredentials = "invalid_credentials";
    public const string AuthenticationRejected = "authentication_rejected";
    public const string UnsupportedPlatform = "unsupported_platform";
    public const string InvalidServer = "invalid_server";
    public const string HttpError = "http_error";
    public const string NetworkError = "network_error";
    public const string RequestTimeout = "request_timeout";
    public const string ResponseTooLarge = "response_too_large";
    public const string InvalidAuthenticationResponse = "invalid_authentication_response";

    /// <summary>The portal is refusing attempts for a while (HTTP 429) after too many wrong tries.</summary>
    public const string TooManyAttempts = "too_many_attempts";
}

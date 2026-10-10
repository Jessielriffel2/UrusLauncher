namespace LegendLauncher.Providers.SwordOnline;

/// <summary>
/// Launcher-side error codes specific to account creation. Transport failures reuse
/// <see cref="SwordOnlineAuthenticationErrorCodes"/>.
/// </summary>
public static class SwordOnlineRegistrationErrorCodes
{
    public const string InvalidLogin = "registration_invalid_login";
    public const string InvalidPassword = "registration_invalid_password";
    public const string InvalidEmail = "registration_invalid_email";
    public const string LoginTaken = "registration_login_taken";
    public const string TooManyRequests = "registration_too_many_requests";
    public const string Rejected = "registration_rejected";
}

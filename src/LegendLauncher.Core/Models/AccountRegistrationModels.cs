namespace LegendLauncher.Core.Models;

/// <summary>
/// Transient input for creating a new account. The password never leaves this object
/// through <see cref="ToString"/>.
/// </summary>
public sealed class AccountRegistrationRequest
{
    public AccountRegistrationRequest(
        PlatformDefinition platform,
        string login,
        string password,
        string? email = null)
    {
        Platform = platform ?? throw new ArgumentNullException(nameof(platform));
        Login = login ?? throw new ArgumentNullException(nameof(login));
        Password = password ?? throw new ArgumentNullException(nameof(password));
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
    }

    public PlatformDefinition Platform { get; }

    public string Login { get; }

    public string Password { get; }

    public string? Email { get; }

    public override string ToString() =>
        $"AccountRegistrationRequest {{ Platform = {Platform.Id}, HasEmail = {Email is not null} }}";
}

/// <summary>
/// Outcome of an account creation attempt, without any secret.
/// </summary>
public sealed class AccountRegistrationResult
{
    private AccountRegistrationResult(string? login, string? errorCode, string? errorMessage)
    {
        Login = login;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess => ErrorCode is null;

    /// <summary>Canonical login of the created account, as spelled by the platform.</summary>
    public string? Login { get; }

    public string? ErrorCode { get; }

    /// <summary>Message supplied by the platform, already bounded; may be <see langword="null"/>.</summary>
    public string? ErrorMessage { get; }

    public static AccountRegistrationResult Success(string login) =>
        new(login ?? throw new ArgumentNullException(nameof(login)), null, null);

    public static AccountRegistrationResult Failure(string errorCode, string? errorMessage = null) =>
        new(null, errorCode ?? throw new ArgumentNullException(nameof(errorCode)), errorMessage);

    public override string ToString() => IsSuccess
        ? "AccountRegistrationResult { IsSuccess = True }"
        : $"AccountRegistrationResult {{ IsSuccess = False, ErrorCode = {ErrorCode} }}";
}

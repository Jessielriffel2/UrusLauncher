namespace LegendLauncher.Core.Models;

/// <summary>
/// Transient input for a platform authentication provider.
/// </summary>
public sealed class AuthenticationRequest
{
    public AuthenticationRequest(
        PlatformDefinition platform,
        GameServer server,
        string loginHint,
        CredentialSecret secret)
    {
        Platform = platform ?? throw new ArgumentNullException(nameof(platform));
        Server = server ?? throw new ArgumentNullException(nameof(server));
        LoginHint = loginHint ?? throw new ArgumentNullException(nameof(loginHint));
        Secret = secret ?? throw new ArgumentNullException(nameof(secret));
    }

    public PlatformDefinition Platform { get; }

    public GameServer Server { get; }

    public string LoginHint { get; }

    public CredentialSecret Secret { get; }

    public override string ToString() =>
        $"AuthenticationRequest {{ Platform = {Platform.Id}, Server = {Server.Id}, HasLoginHint = {LoginHint.Length > 0} }}";
}

/// <summary>
/// Sensitive launch data returned by an authentication provider.
/// </summary>
public sealed class LaunchSession
{
    public LaunchSession(
        Uri launchUri,
        IReadOnlyDictionary<string, string>? parameters = null)
    {
        LaunchUri = launchUri ?? throw new ArgumentNullException(nameof(launchUri));
        Parameters = parameters ?? new Dictionary<string, string>();
    }

    public Uri LaunchUri { get; }

    public IReadOnlyDictionary<string, string> Parameters { get; }

    public override string ToString() =>
        $"LaunchSession {{ ParameterCount = {Parameters.Count} }}";
}

/// <summary>
/// Authentication outcome without exposing session data through logging.
/// </summary>
public sealed class AuthenticationResult
{
    private AuthenticationResult(
        LaunchSession? session,
        long? providerUserId,
        string? errorCode,
        string? errorMessage)
    {
        Session = session;
        ProviderUserId = providerUserId;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess => Session is not null;

    public LaunchSession? Session { get; }

    /// <summary>
    /// Gets the authenticated account identifier assigned by the platform provider.
    /// </summary>
    public long? ProviderUserId { get; }

    public string? ErrorCode { get; }

    public string? ErrorMessage { get; }

    public static AuthenticationResult Success(
        LaunchSession session,
        long? providerUserId = null) =>
        new(
            session ?? throw new ArgumentNullException(nameof(session)),
            providerUserId,
            null,
            null);

    public static AuthenticationResult Failure(string errorCode, string? errorMessage = null) =>
        new(
            null,
            null,
            errorCode ?? throw new ArgumentNullException(nameof(errorCode)),
            errorMessage);

    public override string ToString() => IsSuccess
        ? $"AuthenticationResult {{ IsSuccess = True, HasProviderUserId = {ProviderUserId is not null} }}"
        : $"AuthenticationResult {{ IsSuccess = False, ErrorCode = {ErrorCode} }}";
}

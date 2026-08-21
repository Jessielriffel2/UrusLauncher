namespace LegendLauncher.Core.Models;

/// <summary>
/// Identifies the bounded authentication stage that failed without retaining request data.
/// </summary>
public enum AuthenticationFailurePhase
{
    Passport,
    Launch,
}

/// <summary>
/// Identifies the local transport implementation without exposing its input.
/// </summary>
public enum AuthenticationTransportKind
{
    ManagedHttp,
    SystemCurl,
}

/// <summary>
/// Sanitized failure metadata. It never contains a URI, response body, cookie or credential.
/// </summary>
public sealed record AuthenticationFailureDiagnostic
{
    public AuthenticationFailureDiagnostic(
        AuthenticationFailurePhase phase,
        AuthenticationTransportKind transport,
        int? httpStatusCode = null)
    {
        if (httpStatusCode is < 100 or > 599)
        {
            throw new ArgumentOutOfRangeException(
                nameof(httpStatusCode),
                "HTTP status codes must be between 100 and 599.");
        }

        Phase = phase;
        Transport = transport;
        HttpStatusCode = httpStatusCode;
    }

    public AuthenticationFailurePhase Phase { get; }

    public AuthenticationTransportKind Transport { get; }

    public int? HttpStatusCode { get; }

    public override string ToString() =>
        $"AuthenticationFailureDiagnostic {{ Phase = {Phase}, Transport = {Transport}, HasHttpStatus = {HttpStatusCode is not null}, HttpStatusCode = {HttpStatusCode?.ToString() ?? "None"} }}";
}

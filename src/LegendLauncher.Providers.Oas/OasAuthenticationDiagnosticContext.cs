using LegendLauncher.Core.Models;

namespace LegendLauncher.Providers.Oas;

/// <summary>
/// Per-attempt mutable context containing only bounded, non-sensitive metadata.
/// </summary>
internal sealed class OasAuthenticationDiagnosticContext
{
    public AuthenticationFailureDiagnostic? Current { get; private set; }

    public void Begin(
        AuthenticationFailurePhase phase,
        AuthenticationTransportKind transport) =>
        Current = new AuthenticationFailureDiagnostic(phase, transport);

    public AuthenticationFailureDiagnostic RecordStatus(int statusCode)
    {
        if (Current is null)
        {
            throw new InvalidOperationException("The diagnostic phase was not initialized.");
        }

        Current = new AuthenticationFailureDiagnostic(
            Current.Phase,
            Current.Transport,
            statusCode);
        return Current;
    }
}

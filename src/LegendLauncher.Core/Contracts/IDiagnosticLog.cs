namespace LegendLauncher.Core.Contracts;

/// <summary>
/// Records failures that are not a successful login and game launch.
/// Implementations must never throw and must never persist passwords or tokens.
/// </summary>
public interface IDiagnosticLog
{
    void WriteFailure(
        string operation,
        string summary,
        Exception? exception = null,
        IReadOnlyList<KeyValuePair<string, string?>>? details = null);
}

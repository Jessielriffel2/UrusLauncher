using LegendLauncher.Core.Contracts;

namespace LegendLauncher.Infrastructure.Logging;

public static class DiagnosticLogExtensions
{
    public static void WriteFailure(
        this IDiagnosticLog log,
        string operation,
        string summary,
        Exception? exception,
        params (string Key, string? Value)[] details)
    {
        ArgumentNullException.ThrowIfNull(log);
        log.WriteFailure(
            operation,
            summary,
            exception,
            details.Select(static item => new KeyValuePair<string, string?>(item.Key, item.Value)).ToArray());
    }
}

using LegendLauncher.Core.Contracts;

namespace LegendLauncher.Infrastructure.Logging;

public sealed class NullDiagnosticLog : IDiagnosticLog
{
    public static NullDiagnosticLog Instance { get; } = new();

    public void WriteFailure(
        string operation,
        string summary,
        Exception? exception = null,
        IReadOnlyList<KeyValuePair<string, string?>>? details = null)
    {
    }
}

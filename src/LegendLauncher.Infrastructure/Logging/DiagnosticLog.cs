using LegendLauncher.Core.Contracts;

namespace LegendLauncher.Infrastructure.Logging;

/// <summary>
/// Process-wide diagnostic log. The WPF app and GameHost assign a file log at startup;
/// tests keep the null default so they do not write to the user Documents folder.
/// </summary>
public static class DiagnosticLog
{
    public static IDiagnosticLog Current { get; set; } = NullDiagnosticLog.Instance;
}

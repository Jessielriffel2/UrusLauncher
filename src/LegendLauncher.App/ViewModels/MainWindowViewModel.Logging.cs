using LegendLauncher.Infrastructure.Logging;

namespace LegendLauncher.App.ViewModels;

internal sealed partial class MainWindowViewModel
{
    private void LogFailure(
        string operation,
        string summary,
        Exception? exception = null,
        params (string Key, string? Value)[] details) =>
        _diagnosticLog.WriteFailure(operation, summary, exception, details);
}

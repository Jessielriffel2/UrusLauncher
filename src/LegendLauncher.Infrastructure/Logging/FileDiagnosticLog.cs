using System.Globalization;
using System.Text;
using LegendLauncher.Core.Contracts;

namespace LegendLauncher.Infrastructure.Logging;

public sealed class FileDiagnosticLog : IDiagnosticLog
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
    private readonly object _gate = new();
    private readonly string _logsDirectory;
    private readonly TimeProvider _timeProvider;
    private readonly string _launcherVersion;

    public FileDiagnosticLog(
        string logsDirectory,
        TimeProvider? timeProvider = null,
        string? launcherVersion = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logsDirectory);
        _logsDirectory = Path.GetFullPath(logsDirectory);
        _timeProvider = timeProvider ?? TimeProvider.System;
        _launcherVersion = string.IsNullOrWhiteSpace(launcherVersion)
            ? typeof(FileDiagnosticLog).Assembly.GetName().Version?.ToString() ?? "unknown"
            : launcherVersion.Trim();
    }

    public string LogsDirectory => _logsDirectory;

    public void WriteFailure(
        string operation,
        string summary,
        Exception? exception = null,
        IReadOnlyList<KeyValuePair<string, string?>>? details = null)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(operation);
            ArgumentException.ThrowIfNullOrWhiteSpace(summary);
            DateTimeOffset now = _timeProvider.GetLocalNow();
            string path = Path.Combine(
                _logsDirectory,
                string.Create(CultureInfo.InvariantCulture, $"uruslauncher-{now:yyyyMMdd}.log"));
            string body = DiagnosticLogSanitizer.Redact(
                BuildEntry(now, operation.Trim(), summary.Trim(), exception, details));
            lock (_gate)
            {
                Directory.CreateDirectory(_logsDirectory);
                File.AppendAllText(path, body, Utf8NoBom);
            }
        }
        catch (Exception)
        {
            // Diagnostic logging must never prevent login, catalog, or game launch.
        }
    }

    private string BuildEntry(
        DateTimeOffset timestamp,
        string operation,
        string summary,
        Exception? exception,
        IReadOnlyList<KeyValuePair<string, string?>>? details)
    {
        var builder = new StringBuilder();
        builder.AppendLine("========");
        builder.Append("Time: ").AppendLine(
            timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture));
        builder.Append("Operation: ").AppendLine(operation);
        builder.Append("Summary: ").AppendLine(summary);
        builder.Append("LauncherVersion: ").AppendLine(_launcherVersion);
        builder.Append("OS: ").AppendLine(Environment.OSVersion.ToString());
        builder.Append("Process64Bit: ").AppendLine(
            Environment.Is64BitProcess.ToString(CultureInfo.InvariantCulture));
        builder.Append("Runtime: ").AppendLine(Environment.Version.ToString());
        builder.Append("Machine: ").AppendLine(Environment.MachineName);
        if (details is { Count: > 0 })
        {
            builder.AppendLine("Details:");
            foreach (KeyValuePair<string, string?> detail in details)
            {
                if (string.IsNullOrWhiteSpace(detail.Key))
                {
                    continue;
                }

                builder.Append("  ")
                    .Append(detail.Key.Trim())
                    .Append(": ")
                    .AppendLine(string.IsNullOrWhiteSpace(detail.Value) ? "(empty)" : detail.Value.Trim());
            }
        }

        if (exception is not null)
        {
            builder.AppendLine("Exception:");
            builder.AppendLine(exception.ToString());
            if (exception.HResult != 0)
            {
                builder.Append("HResult: 0x")
                    .AppendLine(exception.HResult.ToString("X8", CultureInfo.InvariantCulture));
            }

            if (exception.Data.Count > 0)
            {
                builder.AppendLine("Exception.Data:");
                foreach (System.Collections.DictionaryEntry entry in exception.Data)
                {
                    builder.Append("  ")
                        .Append(entry.Key)
                        .Append(": ")
                        .AppendLine(entry.Value?.ToString() ?? "(null)");
                }
            }
        }

        builder.AppendLine("========");
        builder.AppendLine();
        return builder.ToString();
    }
}

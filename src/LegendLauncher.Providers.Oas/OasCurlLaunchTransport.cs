using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;

namespace LegendLauncher.Providers.Oas;

/// <summary>
/// Uses the Windows inbox curl only for OAS launch-page requests whose edge rejects
/// the .NET TLS fingerprint. Request addresses and cookies are supplied over stdin.
/// </summary>
internal sealed class OasCurlLaunchTransport
{
    private const int MaximumConfigurableResponseBytes = 16 * 1024 * 1024;
    private const string GenericTransportFailureMessage =
        "The compatible OAS transport could not complete the request.";

    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);
    private static readonly TimeSpan SafetyTimeout = TimeSpan.FromMinutes(5);

    private readonly int _maximumResponseBytes;
    private readonly TimeSpan _requestTimeout;

    public OasCurlLaunchTransport(TimeSpan requestTimeout, int maximumResponseBytes)
    {
        if (requestTimeout != Timeout.InfiniteTimeSpan && requestTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestTimeout),
                "The request timeout must be positive or infinite.");
        }

        if (maximumResponseBytes <= 0 ||
            maximumResponseBytes > MaximumConfigurableResponseBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumResponseBytes),
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The response limit must be between 1 and {MaximumConfigurableResponseBytes} bytes."));
        }

        _requestTimeout = requestTimeout == Timeout.InfiniteTimeSpan ||
            requestTimeout > SafetyTimeout
                ? SafetyTimeout
                : requestTimeout;
        _maximumResponseBytes = maximumResponseBytes;
    }

    public async Task<HttpResponseMessage> SendGetAsync(
        Uri requestUri,
        CookieContainer cookies,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requestUri);
        ArgumentNullException.ThrowIfNull(cookies);
        ValidateRequestUri(requestUri);
        cancellationToken.ThrowIfCancellationRequested();

        var requestConfig = BuildRequestConfig(requestUri, cookies);
        var processStartInfo = CreateProcessStartInfo();
        var output = await ExecuteAsync(
                processStartInfo,
                requestConfig,
                cancellationToken)
            .ConfigureAwait(false);

        return OasCurlResponseParser.Parse(
            output,
            requestUri,
            _maximumResponseBytes);
    }

    internal ProcessStartInfo CreateProcessStartInfo()
    {
        var curlPath = GetTrustedCurlPath();
        var startInfo = new ProcessStartInfo
        {
            FileName = curlPath,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = Utf8WithoutBom,
        };

        AddArguments(startInfo.ArgumentList);
        return startInfo;
    }

    internal static string BuildRequestConfig(Uri requestUri, CookieContainer cookies)
    {
        ArgumentNullException.ThrowIfNull(requestUri);
        ArgumentNullException.ThrowIfNull(cookies);
        ValidateRequestUri(requestUri);

        var config = new StringBuilder(256);
        AppendConfigOption(config, "url", requestUri.AbsoluteUri);
        AppendConfigOption(config, "request", "GET");
        AppendConfigOption(config, "user-agent", "LegendLauncherNext/0.1");
        AppendConfigOption(config, "header", "Accept: text/html");

        var cookieHeader = cookies.GetCookieHeader(requestUri);
        if (!string.IsNullOrEmpty(cookieHeader))
        {
            AppendConfigOption(config, "header", $"Cookie: {cookieHeader}");
        }

        return config.ToString();
    }

    internal static string EscapeConfigValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var escaped = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (character < ' ' || character == '\u007F')
            {
                throw new ArgumentException(
                    "Curl configuration values cannot contain control characters.",
                    nameof(value));
            }

            if (character is '\\' or '"')
            {
                escaped.Append('\\');
            }

            escaped.Append(character);
        }

        return escaped.ToString();
    }

    private async Task<byte[]> ExecuteAsync(
        ProcessStartInfo processStartInfo,
        string requestConfig,
        CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = processStartInfo };
        Task<byte[]>? standardOutputTask = null;
        Task? standardErrorTask = null;

        try
        {
            if (!process.Start())
            {
                throw new HttpRequestException(GenericTransportFailureMessage);
            }

            standardOutputTask = ReadBoundedOutputAsync(process.StandardOutput.BaseStream);
            standardErrorTask = DrainAsync(process.StandardError.BaseStream);

            await process.StandardInput
                .WriteAsync(requestConfig.AsMemory(), cancellationToken)
                .ConfigureAwait(false);
            process.StandardInput.Close();

            var waitForExitTask = process.WaitForExitAsync(cancellationToken);
            var firstCompletedTask = await Task
                .WhenAny(waitForExitTask, standardOutputTask)
                .ConfigureAwait(false);
            if (firstCompletedTask == standardOutputTask && standardOutputTask.IsFaulted)
            {
                await standardOutputTask.ConfigureAwait(false);
            }

            await waitForExitTask.ConfigureAwait(false);

            var output = await standardOutputTask.ConfigureAwait(false);
            await standardErrorTask.ConfigureAwait(false);

            if (process.ExitCode == 63)
            {
                throw new OasResponseTooLargeException();
            }

            if (process.ExitCode != 0)
            {
                throw new HttpRequestException(GenericTransportFailureMessage);
            }

            return output;
        }
        catch (OperationCanceledException)
        {
            TryTerminate(process);
            await ObserveBackgroundTasksAsync(standardOutputTask, standardErrorTask)
                .ConfigureAwait(false);
            throw;
        }
        catch (OasResponseTooLargeException)
        {
            TryTerminate(process);
            await ObserveBackgroundTasksAsync(standardOutputTask, standardErrorTask)
                .ConfigureAwait(false);
            throw;
        }
        catch (HttpRequestException)
        {
            TryTerminate(process);
            await ObserveBackgroundTasksAsync(standardOutputTask, standardErrorTask)
                .ConfigureAwait(false);
            throw;
        }
        catch (Exception exception) when (
            exception is Win32Exception or
            IOException or
            InvalidOperationException or
            UnauthorizedAccessException)
        {
            TryTerminate(process);
            await ObserveBackgroundTasksAsync(standardOutputTask, standardErrorTask)
                .ConfigureAwait(false);
            throw new HttpRequestException(GenericTransportFailureMessage);
        }
    }

    private async Task<byte[]> ReadBoundedOutputAsync(Stream output)
    {
        var maximumCombinedBytes = checked(
            _maximumResponseBytes + OasCurlResponseParser.MaximumHeaderBytes);
        using var buffer = new MemoryStream(Math.Min(maximumCombinedBytes, 64 * 1024));
        var readBuffer = new byte[8192];

        while (true)
        {
            var bytesRead = await output
                .ReadAsync(readBuffer)
                .ConfigureAwait(false);
            if (bytesRead == 0)
            {
                break;
            }

            if (buffer.Length + bytesRead > maximumCombinedBytes)
            {
                throw new OasResponseTooLargeException();
            }

            await buffer
                .WriteAsync(readBuffer.AsMemory(0, bytesRead))
                .ConfigureAwait(false);
        }

        return buffer.ToArray();
    }

    private static async Task DrainAsync(Stream stream)
    {
        var buffer = new byte[4096];
        while (await stream.ReadAsync(buffer).ConfigureAwait(false) != 0)
        {
            // stderr is intentionally drained and discarded so remote data is never surfaced.
        }
    }

    private void AddArguments(ICollection<string> arguments)
    {
        arguments.Add("--disable");
        arguments.Add("-4");
        arguments.Add("--silent");
        arguments.Add("--show-error");
        arguments.Add("--compressed");
        arguments.Add("--http1.1");
        arguments.Add("--proto");
        arguments.Add("=https");
        arguments.Add("--proto-redir");
        arguments.Add("=https");
        arguments.Add("--max-redirs");
        arguments.Add("0");
        arguments.Add("--max-time");
        arguments.Add(_requestTimeout.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture));
        arguments.Add("--max-filesize");
        arguments.Add(_maximumResponseBytes.ToString(CultureInfo.InvariantCulture));
        arguments.Add("--dump-header");
        arguments.Add("-");
        arguments.Add("--output");
        arguments.Add("-");
        arguments.Add("--config");
        arguments.Add("-");
    }

    private static void AppendConfigOption(
        StringBuilder config,
        string option,
        string value)
    {
        config
            .Append(option)
            .Append(" = \"")
            .Append(EscapeConfigValue(value))
            .AppendLine("\"");
    }

    private static string GetTrustedCurlPath()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new HttpRequestException(GenericTransportFailureMessage);
        }

        var systemDirectory = Environment.SystemDirectory;
        var curlPath = Path.Combine(systemDirectory, "curl.exe");
        if (!File.Exists(curlPath))
        {
            throw new HttpRequestException(GenericTransportFailureMessage);
        }

        return curlPath;
    }

    private static void ValidateRequestUri(Uri requestUri)
    {
        if (!OasOriginPolicy.IsAllowedGameUri(requestUri))
        {
            throw new ArgumentException(
                "The compatible OAS transport accepts only allowlisted OAS HTTPS addresses.",
                nameof(requestUri));
        }
    }

    private static void TryTerminate(Process process)
    {
        try
        {
            if (process.HasExited)
            {
                return;
            }

            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or
            Win32Exception or
            NotSupportedException)
        {
            // Best effort only; the original sanitized failure is preserved.
        }
    }

    private static async Task ObserveBackgroundTasksAsync(
        Task<byte[]>? standardOutputTask,
        Task? standardErrorTask)
    {
        try
        {
            if (standardOutputTask is not null)
            {
                await standardOutputTask.ConfigureAwait(false);
            }

            if (standardErrorTask is not null)
            {
                await standardErrorTask.ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (
            exception is IOException or
            ObjectDisposedException or
            OasResponseTooLargeException)
        {
            // Background pipe failures cannot replace the primary transport failure.
        }
    }
}

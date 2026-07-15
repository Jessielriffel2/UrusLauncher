using System.Diagnostics;
using System.Globalization;
using LegendLauncher.Core.Contracts;
using LegendLauncher.Core.Models;

namespace LegendLauncher.GameHost.Legacy;

/// <summary>
/// Starts the isolated x64 GameHost and transfers an authenticated session over protected IPC.
/// </summary>
public sealed class LegacyGameRuntime : IGameRuntime
{
    public static readonly TimeSpan DefaultHandshakeTimeout = TimeSpan.FromSeconds(30);
    private const string ExecutableName = "LegendLauncher.GameHost.Legacy.exe";

    private readonly string _executablePath;
    private readonly TimeSpan _handshakeTimeout;
    private readonly TimeProvider _timeProvider;

    public LegacyGameRuntime(
        string? executablePath = null,
        TimeSpan? handshakeTimeout = null)
        : this(executablePath, handshakeTimeout, TimeProvider.System)
    {
    }

    internal LegacyGameRuntime(
        string? executablePath,
        TimeSpan? handshakeTimeout,
        TimeProvider timeProvider)
    {
        string path = executablePath ?? Path.Combine(AppContext.BaseDirectory, ExecutableName);
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A GameHost executable path is required.", nameof(executablePath));
        }

        _executablePath = Path.GetFullPath(path);
        _handshakeTimeout = handshakeTimeout ?? DefaultHandshakeTimeout;
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        if (_handshakeTimeout <= TimeSpan.Zero && _handshakeTimeout != Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(handshakeTimeout));
        }
    }

    public async Task<GameSession> LaunchAsync(
        LaunchSession session,
        GameRuntimeOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();
        LegacyLaunchUriPolicy.EnsureAllowed(session.LaunchUri);
        EnsureRuntimeIsReady(options);
        session = new LaunchSession(
            session.LaunchUri,
            session.Parameters);

        string pipeName = LaunchSessionPipeIdentity.CreatePipeName();
        string nonce = LaunchSessionPipeIdentity.CreateNonce();
        await using var server = LaunchSessionPipeServer.Create(pipeName);
        using Process process = StartHost(options, pipeName, nonce);

        try
        {
            nint nativeWindowHandle = await server
                .SendAsync(
                    session,
                    nonce,
                    process.Id,
                    _handshakeTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
            GameHostWindowIdentity.EnsureOwnedByProcess(nativeWindowHandle, process.Id);
            return new GameSession(
                process.Id,
                nativeWindowHandle,
                _timeProvider.GetUtcNow());
        }
        catch
        {
            TryTerminate(process);
            throw;
        }
    }

    private void EnsureRuntimeIsReady(GameRuntimeOptions options)
    {
        if (!File.Exists(_executablePath))
        {
            throw new FileNotFoundException("The isolated GameHost executable was not found.");
        }

        if (!Enum.IsDefined(options.Quality) || !Enum.IsDefined(options.WindowMode))
        {
            throw new ArgumentException("The runtime rendering options are invalid.", nameof(options));
        }

        LegacyRuntimeAssets assets = LegacyRuntimeAssets.Discover(options.RuntimeRoot);
        if (!assets.IsComplete)
        {
            throw new InvalidOperationException("The registration-free Flash runtime is incomplete.");
        }
    }

    private Process StartHost(GameRuntimeOptions options, string pipeName, string nonce)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _executablePath,
            WorkingDirectory = Path.GetDirectoryName(_executablePath) ?? AppContext.BaseDirectory,
            UseShellExecute = false,
            ErrorDialog = false,
        };
        startInfo.ArgumentList.Add("--runtime-root");
        startInfo.ArgumentList.Add(options.RuntimeRoot);
        startInfo.ArgumentList.Add("--pipe");
        startInfo.ArgumentList.Add(pipeName);
        startInfo.ArgumentList.Add("--nonce");
        startInfo.ArgumentList.Add(nonce);
        startInfo.ArgumentList.Add("--parent-pid");
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--quality");
        startInfo.ArgumentList.Add(GetQualityArgument(options.Quality));
        startInfo.ArgumentList.Add("--wmode");
        startInfo.ArgumentList.Add(GetWindowModeArgument(options.WindowMode));
        ConfigureLanguageEnvironment(startInfo, CultureInfo.CurrentUICulture);

        return Process.Start(startInfo) ??
            throw new InvalidOperationException("The isolated GameHost process could not be started.");
    }

    internal static void ConfigureLanguageEnvironment(
        ProcessStartInfo startInfo,
        CultureInfo? currentCulture = null)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        string cultureName = GameHostLocalization.NormalizeCultureName(
            (currentCulture ?? CultureInfo.CurrentUICulture).Name);
        startInfo.Environment[GameHostLocalization.EnvironmentVariableName] = cultureName;
    }

    private static string GetQualityArgument(GameRenderQuality quality) => quality switch
    {
        GameRenderQuality.Low => "low",
        GameRenderQuality.AutoLow => "autolow",
        GameRenderQuality.AutoHigh => "autohigh",
        GameRenderQuality.High => "high",
        _ => throw new ArgumentOutOfRangeException(nameof(quality)),
    };

    private static string GetWindowModeArgument(GameWindowMode windowMode) => windowMode switch
    {
        GameWindowMode.Opaque => "opaque",
        GameWindowMode.Direct => "direct",
        _ => throw new ArgumentOutOfRangeException(nameof(windowMode)),
    };

    private static void TryTerminate(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The child already exited between the state check and termination.
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Preserve the original launch or IPC failure.
        }
    }
}

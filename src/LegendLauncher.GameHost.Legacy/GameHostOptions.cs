using LegendLauncher.Core.Models;

namespace LegendLauncher.GameHost.Legacy;

internal sealed record GameHostOptions(
    string RuntimeRoot,
    bool DiagnosticsOnly,
    string? PipeName,
    string? Nonce,
    int? ParentProcessId,
    GameRenderQuality Quality,
    GameWindowMode WindowMode)
{
    public GameRuntimeOptions RuntimeOptions => new(RuntimeRoot, Quality, WindowMode);

    public override string ToString() =>
        $"GameHostOptions {{ DiagnosticsOnly = {DiagnosticsOnly}, HasPipe = {PipeName is not null}, HasNonce = {Nonce is not null}, HasParentProcess = {ParentProcessId is not null}, Quality = {Quality}, WindowMode = {WindowMode} }}";

    public static bool TryParse(
        IReadOnlyList<string> args,
        out GameHostOptions? options,
        out string? error)
    {
        options = null;
        error = null;
        string? runtimeRoot = null;
        string? pipeName = null;
        string? nonce = null;
        int? parentProcessId = null;
        bool diagnosticsOnly = false;
        GameRenderQuality quality = GameRenderQuality.High;
        GameWindowMode windowMode = GameWindowMode.Opaque;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < args.Count; index++)
        {
            string argument = args[index];
            if (!seen.Add(argument))
            {
                error = "Command-line options cannot be repeated.";
                return false;
            }

            switch (argument)
            {
                case "--runtime-root" when TryReadValue(args, ref index, out var root):
                    runtimeRoot = root;
                    break;
                case "--pipe" when TryReadValue(args, ref index, out var pipe):
                    pipeName = pipe;
                    break;
                case "--nonce" when TryReadValue(args, ref index, out var nonceValue):
                    nonce = nonceValue.ToLowerInvariant();
                    break;
                case "--parent-pid" when TryReadValue(args, ref index, out var processValue) &&
                                          int.TryParse(processValue, out var processId) &&
                                          processId > 0:
                    parentProcessId = processId;
                    break;
                case "--quality" when TryReadValue(args, ref index, out var qualityValue) &&
                                      TryParseQuality(qualityValue, out quality):
                    break;
                case "--wmode" when TryReadValue(args, ref index, out var windowModeValue) &&
                                    TryParseWindowMode(windowModeValue, out windowMode):
                    break;
                case "--diagnostics":
                    diagnosticsOnly = true;
                    break;
                case "--url" or "--token" or "--password" or "--session":
                    error = "Secrets and game addresses are never accepted on the command line.";
                    return false;
                default:
                    error = "Unknown, invalid, or incomplete command-line option.";
                    return false;
            }
        }

        if (string.IsNullOrWhiteSpace(runtimeRoot))
        {
            error = "The --runtime-root option is required.";
            return false;
        }

        if (diagnosticsOnly)
        {
            if (pipeName is not null || nonce is not null || parentProcessId is not null)
            {
                error = "Diagnostics mode cannot receive a launch session.";
                return false;
            }
        }
        else if (!LaunchSessionPipeIdentity.IsValidPipeName(pipeName) ||
                 !LaunchSessionPipeIdentity.IsValidNonce(nonce) ||
                 parentProcessId is null)
        {
            error = "Session mode requires a valid --pipe, --nonce, and --parent-pid.";
            return false;
        }

        try
        {
            options = new GameHostOptions(
                Path.GetFullPath(runtimeRoot),
                diagnosticsOnly,
                pipeName,
                nonce,
                parentProcessId,
                quality,
                windowMode);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            error = "The runtime root is invalid.";
            return false;
        }
    }

    private static bool TryReadValue(
        IReadOnlyList<string> args,
        ref int index,
        out string value)
    {
        if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            value = string.Empty;
            return false;
        }

        value = args[++index];
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryParseQuality(string value, out GameRenderQuality quality)
    {
        quality = value.ToLowerInvariant() switch
        {
            "low" => GameRenderQuality.Low,
            "autolow" => GameRenderQuality.AutoLow,
            "autohigh" => GameRenderQuality.AutoHigh,
            "high" => GameRenderQuality.High,
            _ => (GameRenderQuality)(-1),
        };
        return Enum.IsDefined(quality);
    }

    private static bool TryParseWindowMode(string value, out GameWindowMode windowMode)
    {
        windowMode = value.ToLowerInvariant() switch
        {
            "opaque" => GameWindowMode.Opaque,
            "direct" => GameWindowMode.Direct,
            _ => (GameWindowMode)(-1),
        };
        return Enum.IsDefined(windowMode);
    }
}

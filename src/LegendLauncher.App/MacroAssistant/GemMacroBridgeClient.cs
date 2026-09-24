using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LegendLauncher.App.MacroAssistant;

internal sealed class GemMacroBridgeClient : IAsyncDisposable
{
    private readonly MacroBridgeOptions _options;
    private readonly SemaphoreSlim _requestGate = new(1, 1);
    private Process? _process;
    private StreamWriter? _input;
    private StreamReader? _output;
    private bool _disposed;

    public GemMacroBridgeClient(MacroBridgeOptions options)
    {
        _options = options;
    }

    public bool IsConfigured => _options.IsConfigured;

    public async Task<MacroScanResult> AnalyzeAsync(
        ReadOnlyMemory<byte> image,
        MacroMode mode,
        SurfaceRegion crop,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_options.IsConfigured)
        {
            return MacroScanResult.Failure(
                mode,
                "GemMacroAssistant não foi encontrado. Defina GEM_MACRO_ASSISTANT_ROOT ou coloque a pasta ao lado do launcher.");
        }

        await _requestGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureStarted();
            string request = JsonSerializer.Serialize(new
            {
                op = "scan",
                mode = mode == MacroMode.Cosmo ? "cosmo" : "gems",
                image = Convert.ToBase64String(image.Span),
                crop = new
                {
                    x = crop.X,
                    y = crop.Y,
                    width = crop.Width,
                    height = crop.Height,
                },
                strategy = "monte_carlo",
                samples = 48,
                candidates = 40,
            }, JsonOptions);

            await _input!.WriteLineAsync(request.AsMemory(), cancellationToken).ConfigureAwait(false);
            await _input.FlushAsync(cancellationToken).ConfigureAwait(false);

            string? line = await _output!.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                throw new IOException("O processo de visão encerrou sem retornar uma análise.");
            }

            return ParseResponse(line, mode);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or JsonException or System.ComponentModel.Win32Exception)
        {
            ResetProcess();
            return MacroScanResult.Failure(mode, exception.Message);
        }
        finally
        {
            _requestGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _requestGate.WaitAsync().ConfigureAwait(false);
        try
        {
            ResetProcess();
        }
        finally
        {
            _requestGate.Release();
            _requestGate.Dispose();
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private void EnsureStarted()
    {
        if (_process is { HasExited: false })
        {
            return;
        }

        ResetProcess();
        var startInfo = new ProcessStartInfo
        {
            FileName = _options.PythonExecutable,
            WorkingDirectory = _options.ProjectRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("-m");
        startInfo.ArgumentList.Add("gem_macro_assistant.worker");

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        if (!process.Start())
        {
            process.Dispose();
            throw new IOException("Não foi possível iniciar o motor visual Python.");
        }

        _process = process;
        _input = process.StandardInput;
        _output = process.StandardOutput;
        _ = DrainStandardErrorAsync(process);
    }

    private async Task DrainStandardErrorAsync(Process process)
    {
        try
        {
            while (await process.StandardError.ReadLineAsync().ConfigureAwait(false) is not null)
            {
            }
        }
        catch (IOException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void ResetProcess()
    {
        _input = null;
        _output = null;
        Process? process = _process;
        _process = null;
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
        finally
        {
            process.Dispose();
        }
    }

    private static MacroScanResult ParseResponse(string line, MacroMode requestedMode)
    {
        using JsonDocument document = JsonDocument.Parse(line);
        JsonElement root = document.RootElement;
        bool ok = !root.TryGetProperty("ok", out JsonElement okElement) || okElement.GetBoolean();
        if (!ok)
        {
            string error = ReadString(root, "error") ?? "O motor visual retornou uma falha desconhecida.";
            return MacroScanResult.Failure(requestedMode, error);
        }

        bool found = ReadBoolean(root, "found");
        int stableFrames = ReadInt(root, "stableFrames");
        string signature = ReadString(root, "signature") ?? string.Empty;
        double confidence = ReadDouble(root, "confidence");
        MacroMove? move = ParseMove(root);
        MacroAction? action = ParseAction(root);
        string status = BuildStatus(requestedMode, found, move, action, root, error: null);
        return new MacroScanResult(
            true,
            found,
            requestedMode,
            move,
            action,
            stableFrames,
            signature,
            status,
            null,
            confidence);
    }

    private static MacroMove? ParseMove(JsonElement root)
    {
        if (!root.TryGetProperty("move", out JsonElement moveElement) ||
            moveElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        MacroPoint? start = ParsePoint(moveElement, "start");
        MacroPoint? end = ParsePoint(moveElement, "end");
        if (start is null || end is null)
        {
            return null;
        }

        return new MacroMove(
            start.Value,
            end.Value,
            ReadString(moveElement, "reason") ?? "Movimento recomendado",
            ReadDouble(moveElement, "score"),
            ReadInt(moveElement, "cleared"),
            ReadInt(moveElement, "combos"));
    }

    private static MacroAction? ParseAction(JsonElement root)
    {
        if (!root.TryGetProperty("action", out JsonElement actionElement) ||
            actionElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        MacroPoint? click = ParsePoint(actionElement, "click");
        MacroPoint? inputClick = ParsePoint(actionElement, "inputClick");
        MacroPoint? afterClick = ParsePoint(actionElement, "afterClick");
        if (click is null && inputClick is null && afterClick is null)
        {
            return null;
        }

        int cooldownMs = ReadInt(actionElement, "cooldownMs");
        return new MacroAction(
            ReadString(actionElement, "kind") ?? "click",
            ReadString(actionElement, "label") ?? "Ação",
            click,
            inputClick,
            afterClick,
            ReadString(actionElement, "text"),
            ReadBoolean(actionElement, "pressEnter"),
            TimeSpan.FromMilliseconds(Math.Clamp(cooldownMs, 0, 10_000)));
    }

    private static MacroPoint? ParsePoint(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement point) ||
            point.ValueKind != JsonValueKind.Object ||
            !point.TryGetProperty("x", out JsonElement x) ||
            !point.TryGetProperty("y", out JsonElement y) ||
            !x.TryGetDouble(out double xValue) ||
            !y.TryGetDouble(out double yValue))
        {
            return null;
        }

        return new MacroPoint(xValue, yValue);
    }

    private static string BuildStatus(
        MacroMode mode,
        bool found,
        MacroMove? move,
        MacroAction? action,
        JsonElement root,
        string? error)
    {
        if (error is not null)
        {
            return error;
        }

        if (!found)
        {
            return mode == MacroMode.Cosmo ? "Janela do Cosmo não encontrada" : "Tabuleiro não encontrado";
        }

        if (mode == MacroMode.Cosmo)
        {
            return action?.Label ?? $"Cosmo: {ReadInt(root, "filledCount")} itens";
        }

        return move is null ? "Aguardando jogada válida" : move.Reason;
    }

    private static bool ReadBoolean(JsonElement parent, string propertyName) =>
        parent.TryGetProperty(propertyName, out JsonElement value) &&
        value.ValueKind == JsonValueKind.True;

    private static int ReadInt(JsonElement parent, string propertyName) =>
        parent.TryGetProperty(propertyName, out JsonElement value) &&
        value.TryGetInt32(out int result)
            ? result
            : 0;

    private static double ReadDouble(JsonElement parent, string propertyName) =>
        parent.TryGetProperty(propertyName, out JsonElement value) &&
        value.TryGetDouble(out double result)
            ? result
            : 0;

    private static string? ReadString(JsonElement parent, string propertyName) =>
        parent.TryGetProperty(propertyName, out JsonElement value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

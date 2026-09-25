using System.ComponentModel;
using System.IO;

namespace LegendLauncher.App.MacroAssistant;

internal enum MacroMode
{
    Gems,
    Cosmo,
    Clicks,
}

internal enum MacroSessionState
{
    Stopped,
    Configuring,
    Starting,
    Running,
    Paused,
    Error,
}

internal interface IMacroSession : INotifyPropertyChanged, IDisposable
{
    bool IsActive { get; }

    MacroSessionState State { get; }

    string StatusText { get; }

    string SessionTitle { get; }

    bool OverlayVisible { get; set; }

    void SetMode(MacroMode mode);

    void Start();

    void Stop();
}

internal readonly record struct MacroPoint(double X, double Y)
{
    public MacroPoint Offset(double x, double y) => new(X + x, Y + y);
}

internal sealed record MacroMove(
    MacroPoint Start,
    MacroPoint End,
    string Reason,
    double Score,
    int Cleared,
    int Combos)
{
    public MacroMove Offset(double x, double y) =>
        this with { Start = Start.Offset(x, y), End = End.Offset(x, y) };
}

internal sealed record MacroAction(
    string Kind,
    string Label,
    MacroPoint? Click,
    MacroPoint? InputClick,
    MacroPoint? AfterClick,
    string? Text,
    bool PressEnter,
    TimeSpan Cooldown)
{
    public MacroAction Offset(double x, double y) =>
        this with
        {
            Click = Click?.Offset(x, y),
            InputClick = InputClick?.Offset(x, y),
            AfterClick = AfterClick?.Offset(x, y),
        };
}

internal sealed record MacroScanResult(
    bool Ok,
    bool Found,
    MacroMode Mode,
    MacroMove? Move,
    MacroAction? Action,
    int StableFrames,
    string Signature,
    string Status,
    string? Error,
    double Confidence)
{
    public static MacroScanResult Failure(MacroMode mode, string error) =>
        new(false, false, mode, null, null, 0, string.Empty, "Erro", error, 0);

    public MacroScanResult Offset(double x, double y) =>
        this with
        {
            Move = Move?.Offset(x, y),
            Action = Action?.Offset(x, y),
        };
}

internal readonly record struct SurfaceGeometry(int X, int Y, int Width, int Height)
{
    public bool HasArea => Width > 0 && Height > 0;
}

internal readonly record struct SurfaceRegion(int X, int Y, int Width, int Height)
{
    public bool HasArea => Width > 0 && Height > 0;
}

internal readonly record struct ClickMacroSettings(int Count, TimeSpan Interval)
{
    public bool IsInfinite => Count == 0;
}

internal sealed record MacroBridgeOptions(string ProjectRoot, string PythonExecutable)
{
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ProjectRoot) &&
        Directory.Exists(ProjectRoot) &&
        File.Exists(Path.Combine(ProjectRoot, "gem_macro_assistant", "worker.py"));
}

internal static class GemMacroBridgeLocator
{
    public static MacroBridgeOptions Resolve()
    {
        string? configuredRoot = Environment.GetEnvironmentVariable("GEM_MACRO_ASSISTANT_ROOT");
        string[] candidates =
        [
            configuredRoot ?? string.Empty,
            Path.Combine(AppContext.BaseDirectory, "GemMacroAssistant"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "GemMacroAssistant"),
        ];

        string root = candidates
            .Select(TryGetFullPath)
            .OfType<string>()
            .FirstOrDefault(IsWorkerRoot) ?? string.Empty;

        string? python = Environment.GetEnvironmentVariable("GEM_MACRO_PYTHON");
        return new MacroBridgeOptions(root, string.IsNullOrWhiteSpace(python) ? "python" : python);
    }

    private static string? TryGetFullPath(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(candidate);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static bool IsWorkerRoot(string candidate) =>
        Directory.Exists(candidate) &&
        File.Exists(Path.Combine(candidate, "gem_macro_assistant", "worker.py"));
}

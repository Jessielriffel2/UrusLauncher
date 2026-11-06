namespace LegendLauncher.Core.Models;

/// <summary>
/// A credential that exists in memory only while authentication is needed.
/// </summary>
public sealed class CredentialSecret
{
    public CredentialSecret(string userName, string password)
    {
        UserName = userName ?? throw new ArgumentNullException(nameof(userName));
        Password = password ?? throw new ArgumentNullException(nameof(password));
    }

    public string UserName { get; }

    public string Password { get; }

    public override string ToString() =>
        $"CredentialSecret {{ HasUserName = {UserName.Length > 0}, HasPassword = {Password.Length > 0} }}";
}

/// <summary>
/// Rendering quality understood by the isolated legacy game host.
/// </summary>
public enum GameRenderQuality
{
    Low,
    AutoLow,
    AutoHigh,
    High,
}

/// <summary>
/// Window composition mode understood by the isolated legacy game host: how Flash presents its frames.
/// </summary>
public enum GameWindowMode
{
    /// <summary>"Compatible" in the launcher: GDI copies, which the color filter and the game zoom can intercept.</summary>
    Opaque,

    /// <summary>"DirectX" in the launcher: Flash presents through Direct3D (smoother); color filter and zoom do not act.</summary>
    Direct,
}

/// <summary>
/// The one text form of <see cref="GameWindowMode"/>: Flash's own <c>WMode</c> value, also used by the GameHost
/// <c>--wmode</c> argument and by the launcher settings file.
/// </summary>
public static class GameWindowModeText
{
    public const string Opaque = "opaque";
    public const string Direct = "direct";

    public static string Format(GameWindowMode windowMode) => windowMode switch
    {
        GameWindowMode.Opaque => Opaque,
        GameWindowMode.Direct => Direct,
        _ => throw new ArgumentOutOfRangeException(nameof(windowMode)),
    };

    public static bool TryParse(string? value, out GameWindowMode windowMode)
    {
        switch (value?.ToLowerInvariant())
        {
            case Opaque:
                windowMode = GameWindowMode.Opaque;
                return true;
            case Direct:
                windowMode = GameWindowMode.Direct;
                return true;
            default:
                windowMode = default;
                return false;
        }
    }
}

/// <summary>
/// Non-sensitive options used to start an isolated game runtime.
/// </summary>
public sealed class GameRuntimeOptions
{
    /// <param name="runtimeRoot">Folder of the Flash runtime.</param>
    /// <param name="windowMode">Always explicit: the player's choice comes from the launcher settings.</param>
    /// <param name="quality">Flash rendering quality.</param>
    public GameRuntimeOptions(
        string runtimeRoot,
        GameWindowMode windowMode,
        GameRenderQuality quality = GameRenderQuality.High)
    {
        if (string.IsNullOrWhiteSpace(runtimeRoot))
        {
            throw new ArgumentException("A runtime root is required.", nameof(runtimeRoot));
        }

        if (!Enum.IsDefined(windowMode))
        {
            throw new ArgumentOutOfRangeException(nameof(windowMode));
        }

        RuntimeRoot = Path.GetFullPath(runtimeRoot);
        Quality = quality;
        WindowMode = windowMode;
    }

    public string RuntimeRoot { get; }

    public GameRenderQuality Quality { get; }

    public GameWindowMode WindowMode { get; }

    public override string ToString() =>
        $"GameRuntimeOptions {{ Quality = {Quality}, WindowMode = {WindowMode} }}";
}

/// <summary>
/// Identifies an isolated game process and the native window that owns its Flash surface.
/// The window handle is non-sensitive and is validated by the runtime before this model is returned.
/// </summary>
public sealed record GameSession(
    int ProcessId,
    nint NativeWindowHandle,
    DateTimeOffset StartedAtUtc);

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
/// Window composition mode understood by the isolated legacy game host.
/// </summary>
public enum GameWindowMode
{
    Opaque,
    Direct,
}

/// <summary>
/// Non-sensitive options used to start an isolated game runtime.
/// </summary>
public sealed class GameRuntimeOptions
{
    public GameRuntimeOptions(
        string runtimeRoot,
        GameRenderQuality quality = GameRenderQuality.High,
        GameWindowMode windowMode = GameWindowMode.Opaque)
    {
        if (string.IsNullOrWhiteSpace(runtimeRoot))
        {
            throw new ArgumentException("A runtime root is required.", nameof(runtimeRoot));
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

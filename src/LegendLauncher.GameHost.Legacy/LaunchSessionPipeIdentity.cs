using System.Security.Cryptography;

namespace LegendLauncher.GameHost.Legacy;

/// <summary>
/// Creates non-sensitive, single-launch identifiers for the local IPC channel.
/// </summary>
internal static class LaunchSessionPipeIdentity
{
    private const string PipePrefix = "legend-launcher-";
    private const int NonceLength = 32;

    public static string CreatePipeName() => $"{PipePrefix}{Guid.NewGuid():N}";

    public static string CreateNonce() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(NonceLength / 2)).ToLowerInvariant();

    public static bool IsValidPipeName(string? pipeName)
    {
        if (pipeName is null ||
            pipeName.Length <= PipePrefix.Length ||
            pipeName.Length > 96 ||
            !pipeName.StartsWith(PipePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        return pipeName.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
    }

    public static bool IsValidNonce(string? nonce) =>
        nonce is { Length: NonceLength } && nonce.All(char.IsAsciiHexDigit);
}

using System.Security.Cryptography;
using System.Text;

namespace LegendLauncher.GameHost.Legacy;

internal sealed class OneTimeNonceValidator : IDisposable
{
    private readonly byte[] _expectedNonce;
    private int _consumed;
    private int _disposed;

    public OneTimeNonceValidator(string expectedNonce)
    {
        if (!LaunchSessionPipeIdentity.IsValidNonce(expectedNonce))
        {
            throw new ArgumentException("The nonce format is invalid.", nameof(expectedNonce));
        }

        _expectedNonce = Encoding.ASCII.GetBytes(expectedNonce.ToLowerInvariant());
    }

    public bool TryConsume(string? candidate)
    {
        if (Volatile.Read(ref _disposed) != 0 ||
            Interlocked.Exchange(ref _consumed, 1) != 0 ||
            !LaunchSessionPipeIdentity.IsValidNonce(candidate))
        {
            return false;
        }

        byte[] supplied = Encoding.ASCII.GetBytes(candidate!.ToLowerInvariant());
        try
        {
            return CryptographicOperations.FixedTimeEquals(_expectedNonce, supplied);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(supplied);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            CryptographicOperations.ZeroMemory(_expectedNonce);
        }
    }
}

using System.IO.Pipes;
using System.Security.Cryptography;
using LegendLauncher.Core.Models;

namespace LegendLauncher.GameHost.Legacy;

internal static class LaunchSessionPipeClient
{
    public static async Task<LaunchSessionPipeConnection> ConnectAsync(
        string pipeName,
        string nonce,
        int expectedServerProcessId,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (!LaunchSessionPipeIdentity.IsValidPipeName(pipeName) ||
            !LaunchSessionPipeIdentity.IsValidNonce(nonce) ||
            expectedServerProcessId <= 0)
        {
            throw new ArgumentException("The launch-session channel identity is invalid.");
        }

        if (timeout <= TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout != Timeout.InfiniteTimeSpan)
        {
            timeoutSource.CancelAfter(timeout);
        }

        var pipe = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        try
        {
            await pipe.ConnectAsync(timeoutSource.Token).ConfigureAwait(false);
            NamedPipePeerProcess.EnsureServerIs(pipe, expectedServerProcessId);
            byte[] payload = await PipeMessageFraming
                .ReadAsync(pipe, timeoutSource.Token)
                .ConfigureAwait(false);
            try
            {
                using var nonceValidator = new OneTimeNonceValidator(nonce);
                LaunchSession session = LaunchSessionIpcCodec.Deserialize(payload, nonceValidator);
                return new LaunchSessionPipeConnection(pipe, session);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(payload);
            }
        }
        catch
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}

internal sealed class LaunchSessionPipeConnection : IAsyncDisposable
{
    private readonly NamedPipeClientStream _pipe;
    private LaunchSession? _session;
    private int _completionStarted;

    public LaunchSessionPipeConnection(NamedPipeClientStream pipe, LaunchSession session)
    {
        _pipe = pipe ?? throw new ArgumentNullException(nameof(pipe));
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public LaunchSession TakeSession() =>
        Interlocked.Exchange(ref _session, null) ??
        throw new InvalidOperationException("The launch session has already been consumed.");

    public async Task CompleteAsync(
        bool isLoaded,
        nint nativeWindowHandle,
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _completionStarted, 1) != 0)
        {
            throw new InvalidOperationException("The launch-session result has already been sent.");
        }

        try
        {
            await PipeCompletionProtocol
                .WriteAsync(_pipe, isLoaded, nativeWindowHandle, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            await _pipe.DisposeAsync().ConfigureAwait(false);
        }
    }

    public ValueTask DisposeAsync() => _pipe.DisposeAsync();
}

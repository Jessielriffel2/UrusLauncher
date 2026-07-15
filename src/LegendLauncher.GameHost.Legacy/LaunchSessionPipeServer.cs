using System.IO.Pipes;
using System.Security.Cryptography;
using LegendLauncher.Core.Models;

namespace LegendLauncher.GameHost.Legacy;

internal sealed class LaunchSessionPipeServer : IAsyncDisposable
{
    private readonly NamedPipeServerStream _pipe;
    private int _sendStarted;

    private LaunchSessionPipeServer(string pipeName)
    {
        PipeName = pipeName;
        _pipe = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly,
            inBufferSize: 0,
            outBufferSize: 4096);
    }

    public string PipeName { get; }

    public static LaunchSessionPipeServer Create(string pipeName)
    {
        if (!LaunchSessionPipeIdentity.IsValidPipeName(pipeName))
        {
            throw new ArgumentException("The pipe name format is invalid.", nameof(pipeName));
        }

        return new LaunchSessionPipeServer(pipeName);
    }

    public async Task<nint> SendAsync(
        LaunchSession session,
        string nonce,
        int expectedClientProcessId,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _sendStarted, 1) != 0)
        {
            throw new InvalidOperationException("This launch-session channel has already been used.");
        }

        if (timeout <= TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        byte[] payload = LaunchSessionIpcCodec.Serialize(session, nonce);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout != Timeout.InfiniteTimeSpan)
        {
            timeoutSource.CancelAfter(timeout);
        }

        try
        {
            await _pipe.WaitForConnectionAsync(timeoutSource.Token).ConfigureAwait(false);
            NamedPipePeerProcess.EnsureClientIs(_pipe, expectedClientProcessId);
            await PipeMessageFraming
                .WriteAsync(_pipe, payload, timeoutSource.Token)
                .ConfigureAwait(false);
            return await PipeCompletionProtocol
                .EnsureLoadedAsync(_pipe, timeoutSource.Token)
                .ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(payload);
        }
    }

    public ValueTask DisposeAsync() => _pipe.DisposeAsync();
}

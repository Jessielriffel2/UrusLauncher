using LegendLauncher.Core.Models;
using LegendLauncher.GameHost.Legacy;

namespace LegendLauncher.Tests.GameHost;

public sealed class LaunchSessionIpcTests
{
    [Fact]
    public void Identity_CreatesRestrictedPipeNameAndNonce()
    {
        string pipeName = LaunchSessionPipeIdentity.CreatePipeName();
        string nonce = LaunchSessionPipeIdentity.CreateNonce();

        Assert.True(LaunchSessionPipeIdentity.IsValidPipeName(pipeName));
        Assert.True(LaunchSessionPipeIdentity.IsValidNonce(nonce));
        Assert.NotEqual(pipeName, LaunchSessionPipeIdentity.CreatePipeName());
        Assert.NotEqual(nonce, LaunchSessionPipeIdentity.CreateNonce());
        Assert.False(LaunchSessionPipeIdentity.IsValidPipeName("..\\unsafe"));
        Assert.False(LaunchSessionPipeIdentity.IsValidNonce("short"));
    }

    [Fact]
    public void NonceValidator_AcceptsExpectedNonceOnlyOnce()
    {
        string nonce = LaunchSessionPipeIdentity.CreateNonce();
        using var validator = new OneTimeNonceValidator(nonce);

        Assert.True(validator.TryConsume(nonce));
        Assert.False(validator.TryConsume(nonce));
    }

    [Fact]
    public void Codec_RoundTripsSensitiveFieldsWithoutPuttingThemInDiagnostics()
    {
        const string parameterValue = "private-flash-value";
        string nonce = LaunchSessionPipeIdentity.CreateNonce();
        var session = new LaunchSession(
            new Uri("https://s1.creaction-network.com/client/Loading.swf?ticket=private"),
            new Dictionary<string, string> { ["ticket"] = parameterValue });

        byte[] payload = LaunchSessionIpcCodec.Serialize(session, nonce);
        using var nonceValidator = new OneTimeNonceValidator(nonce);
        LaunchSession restored = LaunchSessionIpcCodec.Deserialize(payload, nonceValidator);

        Assert.Equal(session.LaunchUri, restored.LaunchUri);
        Assert.Equal(parameterValue, restored.Parameters["ticket"]);
        Assert.DoesNotContain(parameterValue, session.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(session.LaunchUri.AbsoluteUri, session.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Codec_RejectsWrongNonceWithoutEchoingPayloadSecrets()
    {
        const string secret = "secret-never-in-errors";
        string nonce = LaunchSessionPipeIdentity.CreateNonce();
        byte[] payload = LaunchSessionIpcCodec.Serialize(
            new LaunchSession(
                new Uri($"https://lobr.creaction-network.com/client/Loading.swf?ticket={secret}")),
            nonce);

        using var nonceValidator = new OneTimeNonceValidator(
            LaunchSessionPipeIdentity.CreateNonce());
        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            LaunchSessionIpcCodec.Deserialize(payload, nonceValidator));

        Assert.DoesNotContain(secret, error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Framing_RejectsOversizedLengthBeforeAllocatingPayload()
    {
        byte[] prefix = BitConverter.GetBytes(LaunchSessionIpcCodec.MaxPayloadBytes + 1);
        await using var stream = new MemoryStream(prefix);

        InvalidDataException error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            PipeMessageFraming.ReadAsync(stream, CancellationToken.None));

        Assert.Contains("size", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Pipe_TransfersOneSessionForCurrentUserAndBoundProcess()
    {
        string pipeName = LaunchSessionPipeIdentity.CreatePipeName();
        string nonce = LaunchSessionPipeIdentity.CreateNonce();
        var session = new LaunchSession(
            new Uri("https://lobr.creaction-network.com/client/Loading.swf"),
            new Dictionary<string, string> { ["sid"] = "42" });
        await using var server = LaunchSessionPipeServer.Create(pipeName);

        nint expectedWindowHandle = new(0x123456);
        Task<nint> send = server.SendAsync(
            session,
            nonce,
            Environment.ProcessId,
            TimeSpan.FromSeconds(5),
            CancellationToken.None);
        await using LaunchSessionPipeConnection connection = await LaunchSessionPipeClient.ConnectAsync(
            pipeName,
            nonce,
            Environment.ProcessId,
            TimeSpan.FromSeconds(5));
        LaunchSession received = connection.TakeSession();
        Assert.Throws<InvalidOperationException>(() => connection.TakeSession());
        await connection.CompleteAsync(isLoaded: true, expectedWindowHandle);
        nint actualWindowHandle = await send;

        Assert.Equal(session.LaunchUri, received.LaunchUri);
        Assert.Equal(expectedWindowHandle, actualWindowHandle);
        await Assert.ThrowsAsync<InvalidOperationException>(() => server.SendAsync(
            session,
            nonce,
            Environment.ProcessId,
            TimeSpan.FromSeconds(1),
            CancellationToken.None));
    }

    [Fact]
    public async Task Pipe_DoesNotReportSuccessWhenGameHostRejectsLoad()
    {
        string pipeName = LaunchSessionPipeIdentity.CreatePipeName();
        string nonce = LaunchSessionPipeIdentity.CreateNonce();
        var session = new LaunchSession(
            new Uri("https://lobr.creaction-network.com/client/Loading.swf"));
        await using var server = LaunchSessionPipeServer.Create(pipeName);

        Task<nint> send = server.SendAsync(
            session,
            nonce,
            Environment.ProcessId,
            TimeSpan.FromSeconds(5),
            CancellationToken.None);
        await using LaunchSessionPipeConnection connection = await LaunchSessionPipeClient.ConnectAsync(
            pipeName,
            nonce,
            Environment.ProcessId,
            TimeSpan.FromSeconds(5));
        _ = connection.TakeSession();
        await connection.CompleteAsync(isLoaded: false, nint.Zero);

        await Assert.ThrowsAsync<InvalidOperationException>(() => send);
    }

    [Fact]
    public async Task CompletionProtocol_RejectsUnknownVersion()
    {
        nint expectedWindowHandle = new(0x778899);
        await using var encoded = new MemoryStream();
        await PipeCompletionProtocol.WriteAsync(
            encoded,
            isLoaded: true,
            expectedWindowHandle,
            CancellationToken.None);
        byte[] response = encoded.ToArray();
        response[4]++;
        await using var corrupted = new MemoryStream(response);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            PipeCompletionProtocol.EnsureLoadedAsync(corrupted, CancellationToken.None));
    }

    [Fact]
    public async Task CompletionProtocol_RejectsSuccessfulResponseWithoutWindow()
    {
        await using var stream = new MemoryStream();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            PipeCompletionProtocol.WriteAsync(
                stream,
                isLoaded: true,
                nint.Zero,
                CancellationToken.None));
    }

    [Fact]
    public async Task CompletionProtocol_RejectsTruncatedResponse()
    {
        await using var stream = new MemoryStream(new byte[PipeCompletionProtocol.ResponseLength - 1]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            PipeCompletionProtocol.EnsureLoadedAsync(stream, CancellationToken.None));
    }
}

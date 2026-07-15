using System.Text.Json;
using System.Text.Json.Serialization;
using LegendLauncher.Core.Models;

namespace LegendLauncher.GameHost.Legacy;

internal static class LaunchSessionIpcCodec
{
    public const int MaxPayloadBytes = 64 * 1024;
    private const int ProtocolVersion = 1;
    private const int MaxLaunchUriLength = 8 * 1024;
    private const int MaxParameterCount = 64;
    private const int MaxParameterNameLength = 128;
    private const int MaxParameterValueLength = 4 * 1024;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        MaxDepth = 8,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static byte[] Serialize(LaunchSession session, string nonce)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!LaunchSessionPipeIdentity.IsValidNonce(nonce))
        {
            throw new ArgumentException("The nonce format is invalid.", nameof(nonce));
        }

        ValidateSessionShape(session);
        var envelope = new LaunchSessionEnvelope
        {
            Version = ProtocolVersion,
            Nonce = nonce.ToLowerInvariant(),
            LaunchUri = session.LaunchUri.AbsoluteUri,
            Parameters = new Dictionary<string, string>(session.Parameters, StringComparer.Ordinal),
        };

        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(envelope, SerializerOptions);
        if (payload.Length > MaxPayloadBytes)
        {
            throw new InvalidDataException("The launch session exceeds the IPC size limit.");
        }

        return payload;
    }

    public static LaunchSession Deserialize(
        ReadOnlySpan<byte> payload,
        OneTimeNonceValidator nonceValidator)
    {
        ArgumentNullException.ThrowIfNull(nonceValidator);
        if (payload.IsEmpty || payload.Length > MaxPayloadBytes)
        {
            throw new InvalidDataException("The launch session payload size is invalid.");
        }

        LaunchSessionEnvelope envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<LaunchSessionEnvelope>(payload, SerializerOptions)
                ?? throw new JsonException();
        }
        catch (JsonException)
        {
            throw new InvalidDataException("The launch session payload is invalid.");
        }

        if (envelope.Version != ProtocolVersion || !nonceValidator.TryConsume(envelope.Nonce))
        {
            throw new InvalidDataException("The launch session handshake is invalid or expired.");
        }

        if (!Uri.TryCreate(envelope.LaunchUri, UriKind.Absolute, out var launchUri))
        {
            throw new InvalidDataException("The launch session address is invalid.");
        }

        var parameters = envelope.Parameters ?? new Dictionary<string, string>();
        var session = new LaunchSession(launchUri, parameters);
        try
        {
            ValidateSessionShape(session);
        }
        catch (ArgumentException)
        {
            throw new InvalidDataException("The launch session fields are invalid.");
        }

        return session;
    }

    private static void ValidateSessionShape(LaunchSession session)
    {
        if (!session.LaunchUri.IsAbsoluteUri ||
            session.LaunchUri.AbsoluteUri.Length > MaxLaunchUriLength)
        {
            throw new ArgumentException("The launch address is invalid.", nameof(session));
        }

        if (session.Parameters.Count > MaxParameterCount)
        {
            throw new ArgumentException("There are too many session parameters.", nameof(session));
        }

        foreach (var parameter in session.Parameters)
        {
            if (string.IsNullOrWhiteSpace(parameter.Key) ||
                parameter.Key.Length > MaxParameterNameLength ||
                parameter.Value is null ||
                parameter.Value.Length > MaxParameterValueLength)
            {
                throw new ArgumentException("A session parameter is invalid.", nameof(session));
            }
        }
    }

    private sealed class LaunchSessionEnvelope
    {
        public int Version { get; init; }

        public string? Nonce { get; init; }

        public string? LaunchUri { get; init; }

        public Dictionary<string, string>? Parameters { get; init; }

        public override string ToString() =>
            $"LaunchSessionEnvelope {{ Version = {Version}, ParameterCount = {Parameters?.Count ?? 0} }}";
    }
}

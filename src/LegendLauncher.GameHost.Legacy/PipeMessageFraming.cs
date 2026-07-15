using System.Buffers.Binary;

namespace LegendLauncher.GameHost.Legacy;

internal static class PipeMessageFraming
{
    private const int PrefixLength = sizeof(int);

    public static async Task WriteAsync(
        Stream stream,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (payload.IsEmpty || payload.Length > LaunchSessionIpcCodec.MaxPayloadBytes)
        {
            throw new InvalidDataException("The launch session payload size is invalid.");
        }

        byte[] prefix = new byte[PrefixLength];
        BinaryPrimitives.WriteInt32LittleEndian(prefix, payload.Length);
        await stream.WriteAsync(prefix, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<byte[]> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        byte[] prefix = new byte[PrefixLength];
        await ReadExactlyAsync(stream, prefix, cancellationToken).ConfigureAwait(false);
        int payloadLength = BinaryPrimitives.ReadInt32LittleEndian(prefix);
        if (payloadLength <= 0 || payloadLength > LaunchSessionIpcCodec.MaxPayloadBytes)
        {
            throw new InvalidDataException("The launch session payload size is invalid.");
        }

        byte[] payload = new byte[payloadLength];
        await ReadExactlyAsync(stream, payload, cancellationToken).ConfigureAwait(false);
        return payload;
    }

    private static async Task ReadExactlyAsync(
        Stream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = await stream
                .ReadAsync(buffer[offset..], cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                throw new InvalidDataException("The launch session frame is incomplete.");
            }

            offset += read;
        }
    }
}

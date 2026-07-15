using System.Buffers.Binary;

namespace LegendLauncher.GameHost.Legacy;

internal static class PipeCompletionProtocol
{
    internal const int ResponseLength = 16;

    private const uint Magic = 0x5348474C; // "LGHS" in little-endian byte order.
    private const byte ProtocolVersion = 1;
    private const byte Loaded = 0xA5;
    private const byte Rejected = 0x5A;
    private const int MagicOffset = 0;
    private const int VersionOffset = 4;
    private const int StatusOffset = 5;
    private const int ReservedOffset = 6;
    private const int WindowHandleOffset = 8;

    public static async Task WriteAsync(
        Stream stream,
        bool isLoaded,
        nint nativeWindowHandle,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (isLoaded != (nativeWindowHandle != nint.Zero))
        {
            throw new ArgumentException(
                "A successful GameHost response requires a native window handle, and a rejection cannot include one.",
                nameof(nativeWindowHandle));
        }

        byte[] response = new byte[ResponseLength];
        BinaryPrimitives.WriteUInt32LittleEndian(
            response.AsSpan(MagicOffset, sizeof(uint)),
            Magic);
        response[VersionOffset] = ProtocolVersion;
        response[StatusOffset] = isLoaded ? Loaded : Rejected;
        BinaryPrimitives.WriteInt64LittleEndian(
            response.AsSpan(WindowHandleOffset, sizeof(long)),
            nativeWindowHandle.ToInt64());

        await stream.WriteAsync(response, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<nint> EnsureLoadedAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        byte[] response = new byte[ResponseLength];
        await ReadExactlyAsync(stream, response, cancellationToken).ConfigureAwait(false);

        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(
            response.AsSpan(MagicOffset, sizeof(uint)));
        ushort reserved = BinaryPrimitives.ReadUInt16LittleEndian(
            response.AsSpan(ReservedOffset, sizeof(ushort)));
        long rawWindowHandle = BinaryPrimitives.ReadInt64LittleEndian(
            response.AsSpan(WindowHandleOffset, sizeof(long)));
        bool hasKnownStatus = response[StatusOffset] is Loaded or Rejected;
        bool hasValidEnvelope = magic == Magic &&
            response[VersionOffset] == ProtocolVersion &&
            reserved == 0 &&
            hasKnownStatus;

        if (!hasValidEnvelope ||
            response[StatusOffset] != Loaded ||
            rawWindowHandle == 0)
        {
            throw CreateLoadFailure();
        }

        try
        {
            return checked((nint)rawWindowHandle);
        }
        catch (OverflowException)
        {
            throw CreateLoadFailure();
        }
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
                throw CreateLoadFailure();
            }

            offset += read;
        }
    }

    private static InvalidOperationException CreateLoadFailure() =>
        new("The isolated GameHost did not confirm the Flash load request and native window.");
}

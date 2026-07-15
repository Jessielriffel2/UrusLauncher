using System.Text;

namespace LegendLauncher.Providers.Oas;

internal static class BoundedHttpContentReader
{
    private const int BufferSize = 8192;

    public static async Task<string> ReadUtf8Async(
        HttpContent content,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is long contentLength &&
            contentLength > maximumBytes)
        {
            throw new OasResponseTooLargeException();
        }

        await using var contentStream = await content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using var bufferStream = new MemoryStream(
            content.Headers.ContentLength is > 0 and <= int.MaxValue
                ? (int)Math.Min(content.Headers.ContentLength.Value, maximumBytes)
                : 0);

        var buffer = new byte[BufferSize];
        while (true)
        {
            var bytesRead = await contentStream
                .ReadAsync(buffer, cancellationToken)
                .ConfigureAwait(false);
            if (bytesRead == 0)
            {
                break;
            }

            if (bufferStream.Length + bytesRead > maximumBytes)
            {
                throw new OasResponseTooLargeException();
            }

            await bufferStream
                .WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken)
                .ConfigureAwait(false);
        }

        return Encoding.UTF8
            .GetString(bufferStream.GetBuffer(), 0, checked((int)bufferStream.Length))
            .TrimStart('\uFEFF');
    }
}

internal sealed class OasResponseTooLargeException : Exception;

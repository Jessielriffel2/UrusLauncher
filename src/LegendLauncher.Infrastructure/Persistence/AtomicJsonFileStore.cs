using System.Collections.Concurrent;
using System.Text.Json;

namespace LegendLauncher.Infrastructure.Persistence;

/// <summary>
/// Reads and writes one JSON document. Writes use a temporary file followed by a
/// same-directory rename, so readers observe either the previous or the complete new document.
/// </summary>
public sealed class AtomicJsonFileStore<TDocument>
    where TDocument : notnull
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> PathLocks =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly string _filePath;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly SemaphoreSlim _pathLock;

    public AtomicJsonFileStore(string filePath, JsonSerializerOptions? serializerOptions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        _filePath = Path.GetFullPath(filePath);
        _serializerOptions = serializerOptions is null
            ? CreateDefaultSerializerOptions()
            : new JsonSerializerOptions(serializerOptions);
        _pathLock = PathLocks.GetOrAdd(_filePath, static _ => new SemaphoreSlim(1, 1));
    }

    public string FilePath => _filePath;

    public bool Exists => File.Exists(_filePath);

    public async Task<TDocument?> ReadAsync(CancellationToken cancellationToken = default)
    {
        await _pathLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ReadCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _pathLock.Release();
        }
    }

    public async Task WriteAsync(TDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        await _pathLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await WriteCoreAsync(document, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _pathLock.Release();
        }
    }

    /// <summary>
    /// Atomically performs a read/modify/write operation for all store instances in this process.
    /// The updater must not perform blocking or re-entrant work.
    /// </summary>
    public async Task UpdateAsync(
        Func<TDocument?, TDocument> updater,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(updater);

        await _pathLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = await ReadCoreAsync(cancellationToken).ConfigureAwait(false);
            var updated = updater(current);
            ArgumentNullException.ThrowIfNull(updated);
            await WriteCoreAsync(updated, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _pathLock.Release();
        }
    }

    public async Task<bool> DeleteAsync(CancellationToken cancellationToken = default)
    {
        await _pathLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_filePath))
            {
                return false;
            }

            File.Delete(_filePath);
            return true;
        }
        finally
        {
            _pathLock.Release();
        }
    }

    private async Task<TDocument?> ReadCoreAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return default;
        }

        await using var stream = new FileStream(
            _filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 16 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        return await JsonSerializer.DeserializeAsync<TDocument>(
            stream,
            _serializerOptions,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteCoreAsync(TDocument document, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException("The JSON file must have a parent directory.");
        Directory.CreateDirectory(directory);

        var temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(_filePath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 16 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    document,
                    _serializerOptions,
                    cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static JsonSerializerOptions CreateDefaultSerializerOptions() =>
        new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
}

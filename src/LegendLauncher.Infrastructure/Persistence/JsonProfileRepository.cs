using System.Text.Json;

namespace LegendLauncher.Infrastructure.Persistence;

/// <summary>
/// Atomic JSON repository for profile-like records. A key selector keeps this adapter
/// independent of UI concerns and prevents duplicate profile identifiers.
/// </summary>
public sealed class JsonProfileRepository<TProfile, TKey>
    where TProfile : notnull
    where TKey : notnull
{
    private readonly AtomicJsonFileStore<TProfile[]> _store;
    private readonly Func<TProfile, TKey> _keySelector;
    private readonly IEqualityComparer<TKey> _keyComparer;

    public JsonProfileRepository(
        string filePath,
        Func<TProfile, TKey> keySelector,
        IEqualityComparer<TKey>? keyComparer = null,
        JsonSerializerOptions? serializerOptions = null)
    {
        ArgumentNullException.ThrowIfNull(keySelector);

        _store = new AtomicJsonFileStore<TProfile[]>(filePath, serializerOptions);
        _keySelector = keySelector;
        _keyComparer = keyComparer ?? EqualityComparer<TKey>.Default;
    }

    public async Task<IReadOnlyList<TProfile>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var profiles = await _store.ReadAsync(cancellationToken).ConfigureAwait(false);
        return profiles is null ? [] : profiles.ToArray();
    }

    public async Task<TProfile?> FindAsync(
        TKey key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        var profiles = await _store.ReadAsync(cancellationToken).ConfigureAwait(false);
        return profiles is null
            ? default
            : profiles.FirstOrDefault(profile => _keyComparer.Equals(_keySelector(profile), key));
    }

    public Task UpsertAsync(TProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var key = _keySelector(profile);
        ArgumentNullException.ThrowIfNull(key);

        return _store.UpdateAsync(
            current => Upsert(current ?? [], key, profile),
            cancellationToken);
    }

    public async Task<bool> DeleteAsync(TKey key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        var removed = false;

        await _store.UpdateAsync(
            current =>
            {
                if (current is null || current.Length == 0)
                {
                    return [];
                }

                var retained = current
                    .Where(profile => !_keyComparer.Equals(_keySelector(profile), key))
                    .ToArray();
                removed = retained.Length != current.Length;
                return retained;
            },
            cancellationToken).ConfigureAwait(false);

        return removed;
    }

    private TProfile[] Upsert(TProfile[] profiles, TKey key, TProfile replacement)
    {
        var index = Array.FindIndex(
            profiles,
            profile => _keyComparer.Equals(_keySelector(profile), key));

        if (index < 0)
        {
            return [.. profiles, replacement];
        }

        var updated = profiles.ToArray();
        updated[index] = replacement;
        return updated;
    }
}

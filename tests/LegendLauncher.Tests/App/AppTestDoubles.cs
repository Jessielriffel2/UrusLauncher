using LegendLauncher.Core.Contracts;
using LegendLauncher.Core.Models;
using LegendLauncher.Infrastructure.Runtime;
using LegendLauncher.Providers.Oas;

namespace LegendLauncher.Tests.App;

internal sealed class InMemoryProfileStore : IProfileStore
{
    private readonly Dictionary<Guid, AccountProfile> _profiles = [];

    public InMemoryProfileStore(params AccountProfile[] profiles)
    {
        foreach (AccountProfile profile in profiles)
        {
            _profiles[profile.Id] = profile;
        }
    }

    public bool ThrowOnSave { get; set; }

    public Func<CancellationToken, Task<IReadOnlyList<AccountProfile>>>? GetAllAsyncOverride { get; set; }

    public Func<AccountProfile, CancellationToken, Task>? SaveAsyncOverride { get; set; }

    public IReadOnlyCollection<AccountProfile> Values => _profiles.Values;

    public Task<IReadOnlyList<AccountProfile>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (GetAllAsyncOverride is not null)
        {
            return GetAllAsyncOverride(cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<AccountProfile>>(_profiles.Values.ToArray());
    }

    public Task<AccountProfile?> GetAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_profiles.GetValueOrDefault(profileId));
    }

    public Task SaveAsync(AccountProfile profile, CancellationToken cancellationToken = default)
    {
        if (SaveAsyncOverride is not null)
        {
            return SaveAsyncOverride(profile, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (ThrowOnSave)
        {
            throw new IOException("profile store unavailable");
        }

        _profiles[profile.Id] = profile;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _profiles.Remove(profileId);
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryCredentialVault : ICredentialVault
{
    private readonly Dictionary<string, CredentialSecret> _credentials =
        new(StringComparer.Ordinal);

    public bool ThrowOnSet { get; set; }

    public int SetCount { get; private set; }

    public int DeleteCount { get; private set; }

    public void Seed(string key, CredentialSecret credential) => _credentials[key] = credential;

    public bool Contains(string key) => _credentials.ContainsKey(key);

    public Task<CredentialSecret?> GetAsync(string credentialKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_credentials.GetValueOrDefault(credentialKey));
    }

    public Task SetAsync(
        string credentialKey,
        CredentialSecret credential,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SetCount++;
        if (ThrowOnSet)
        {
            throw new IOException("credential vault unavailable");
        }

        _credentials[credentialKey] = credential;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string credentialKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DeleteCount++;
        _credentials.Remove(credentialKey);
        return Task.CompletedTask;
    }
}

internal sealed class StubAuthenticationService(
    Func<AuthenticationRequest, CancellationToken, Task<AuthenticationResult>> authenticate)
    : IGameAuthenticationService
{
    public List<AuthenticationRequest> Requests { get; } = [];

    public Task<AuthenticationResult> AuthenticateAsync(
        AuthenticationRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return authenticate(request, cancellationToken);
    }
}

internal sealed class StubGameRuntime : IGameRuntime
{
    public List<LaunchSession> Sessions { get; } = [];

    public Task<GameSession> LaunchAsync(
        LaunchSession session,
        GameRuntimeOptions options,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Sessions.Add(session);
        return Task.FromResult(new GameSession(4242, new nint(0x1234), DateTimeOffset.UnixEpoch));
    }
}

internal sealed class StubServerDirectory(
    Func<PlatformDefinition, long, CancellationToken, Task<ServerCatalog>> load)
    : IServerDirectory
{
    public List<(string PlatformId, long UserId)> Requests { get; } = [];

    public Task<ServerCatalog> GetServersAsync(
        PlatformDefinition platform,
        long userId = 0,
        CancellationToken cancellationToken = default)
    {
        Requests.Add((platform.Id, userId));
        return load(platform, userId, cancellationToken);
    }
}

internal static class AppTestData
{
    public static readonly DateTimeOffset Now =
        new(2026, 7, 14, 18, 0, 0, TimeSpan.Zero);

    public static GameServer Server(
        string id = "3257",
        bool recommended = false,
        bool valid = true,
        DateTimeOffset? opensAt = null) =>
        new(
            id,
            long.Parse(id, System.Globalization.CultureInfo.InvariantCulture),
            $"S{id}",
            $"Servidor {id}",
            $"OAS{id}: Servidor {id}",
            new Uri($"https://lobr.creaction-network.com/serverlist/s{id}"),
            recommended,
            valid,
            null,
            opensAt ?? Now.AddDays(-1));

    public static ServerCatalog Catalog(
        IReadOnlyList<GameServer> all,
        IReadOnlyList<GameServer>? played = null,
        GameServer? current = null) =>
        new(
            OasPlatformCatalog.Brazil.Id,
            all,
            played ?? [],
            current,
            Now);

    public static AccountProfile Profile(
        string userName,
        long? providerUserId,
        string? lastServerId,
        int updatedOffset = 0)
    {
        Guid id = Guid.NewGuid();
        return new AccountProfile(
            id,
            userName,
            OasPlatformCatalog.Brazil.Id,
            userName,
            $"LegendLauncherNext/profile/{id:N}",
            providerUserId,
            lastServerId,
            Now.AddDays(-10),
            Now.AddMinutes(updatedOffset));
    }

    public static LegacyRuntimeProbeResult UsableRuntime() =>
        new(
            true,
            "C:\\legacy-runtime",
            "C:\\legacy-runtime\\Adobe.Flash.Control.manifest",
            "C:\\legacy-runtime\\flash\\Flash.ocx",
            [],
            LegacyRuntimeProbeSource.ConfiguredPath);
}

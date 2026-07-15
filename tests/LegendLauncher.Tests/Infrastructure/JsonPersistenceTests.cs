using LegendLauncher.Core.Models;
using LegendLauncher.Infrastructure.Persistence;
using LegendLauncher.Infrastructure.Security;

namespace LegendLauncher.Tests.Infrastructure;

public sealed class JsonPersistenceTests
{
    [Fact]
    public async Task ProfileRepository_UpsertsFindsAndDeletesProfiles()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var repository = new JsonProfileRepository<TestProfile, Guid>(
            temporaryDirectory.Combine("profiles.json"),
            profile => profile.Id);
        var id = Guid.NewGuid();

        await repository.UpsertAsync(new TestProfile(id, "First"));
        await repository.UpsertAsync(new TestProfile(id, "Updated"));

        Assert.Equal(new TestProfile(id, "Updated"), await repository.FindAsync(id));
        Assert.Single(await repository.GetAllAsync());
        Assert.True(await repository.DeleteAsync(id));
        Assert.False(await repository.DeleteAsync(id));
        Assert.Empty(await repository.GetAllAsync());
    }

    [Fact]
    public async Task ServerCatalogCache_SeparatesPlatformsAndMarksReadsAsCached()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var cache = new JsonServerCatalogCache(temporaryDirectory.Combine("catalogs.json"));
        var first = CreateCatalog("oas");
        var second = CreateCatalog("partner");

        await cache.SetAsync(first);
        await cache.SetAsync(second);

        var cachedFirst = await cache.GetAsync("OAS");
        var cachedSecond = await cache.GetAsync("partner");
        Assert.NotNull(cachedFirst);
        Assert.NotNull(cachedSecond);
        Assert.True(cachedFirst.IsFromCache);
        Assert.True(cachedSecond.IsFromCache);
        Assert.Equal(first.PlatformId, cachedFirst.PlatformId);
        Assert.Equal(first.All, cachedFirst.All);
        Assert.Equal(second.PlatformId, cachedSecond.PlatformId);
        Assert.Null(await cache.GetAsync("missing"));
    }

    [Fact]
    public async Task ProfileStore_PersistsOnlyNonSecretProfileData()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var filePath = temporaryDirectory.Combine("profiles.json");
        var store = new JsonProfileStore(filePath);
        var profileId = Guid.NewGuid();
        var profile = new AccountProfile(
            profileId,
            "Primary account",
            "oas",
            "player@example.test",
            CredentialKey.ForProfile(profileId),
            42,
            "7",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow)
        {
            RecentServerIds = ["7", "6"],
        };

        await store.SaveAsync(profile);

        AccountProfile loaded = Assert.IsType<AccountProfile>(await store.GetAsync(profileId));
        Assert.Equal(profile.Id, loaded.Id);
        Assert.Equal(profile.DisplayName, loaded.DisplayName);
        Assert.Equal(profile.PlatformId, loaded.PlatformId);
        Assert.Equal(profile.UserName, loaded.UserName);
        Assert.Equal(profile.CredentialKey, loaded.CredentialKey);
        Assert.Equal(profile.ProviderUserId, loaded.ProviderUserId);
        Assert.Equal(profile.LastServerId, loaded.LastServerId);
        Assert.Equal(profile.RecentServerIds, loaded.RecentServerIds);
        var persistedJson = await File.ReadAllTextAsync(filePath);
        Assert.DoesNotContain("password", persistedJson, StringComparison.OrdinalIgnoreCase);
        await store.DeleteAsync(profileId);
        Assert.Empty(await store.GetAllAsync());
    }

    [Fact]
    public async Task ProfileStore_LoadsLegacyProfileWithoutRecentServerIds()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var filePath = temporaryDirectory.Combine("profiles.json");
        var profileId = Guid.NewGuid();
        string credentialKey = CredentialKey.ForProfile(profileId);
        string json = $$"""
            [
              {
                "id": "{{profileId}}",
                "displayName": "Legacy profile",
                "platformId": "oas-lobr",
                "userName": "legacy@example.test",
                "credentialKey": "{{credentialKey}}",
                "providerUserId": 42,
                "lastServerId": "7",
                "createdAtUtc": "2026-07-01T12:00:00+00:00",
                "updatedAtUtc": "2026-07-02T12:00:00+00:00"
              }
            ]
            """;
        await File.WriteAllTextAsync(filePath, json);
        var store = new JsonProfileStore(filePath);

        AccountProfile loaded = Assert.Single(await store.GetAllAsync());

        Assert.Equal(profileId, loaded.Id);
        Assert.Equal("7", loaded.LastServerId);
        Assert.NotNull(loaded.RecentServerIds);
        Assert.Empty(loaded.RecentServerIds);
    }

    [Fact]
    public async Task ProfileStore_RejectsCredentialKeysOwnedByAnotherClient()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var store = new JsonProfileStore(temporaryDirectory.Combine("profiles.json"));
        var profile = new AccountProfile(
            Guid.NewGuid(),
            "Legacy",
            "oas",
            "player",
            "OldLegendClient/player",
            null,
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(profile));
    }

    public sealed record TestProfile(Guid Id, string Name);

    private static ServerCatalog CreateCatalog(string platformId) =>
        new(
            platformId,
            [new GameServer("1", "Server 1")],
            [],
            null,
            DateTimeOffset.UtcNow);
}

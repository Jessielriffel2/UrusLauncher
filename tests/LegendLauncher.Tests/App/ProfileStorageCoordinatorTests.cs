using LegendLauncher.Core.Contracts;
using LegendLauncher.App.Services;
using LegendLauncher.Core.Models;
using LegendLauncher.Infrastructure.Security;
using LegendLauncher.Providers.Oas;
using LegendLauncher.Providers.SevenWan;

namespace LegendLauncher.Tests.App;

public sealed class ProfileStorageCoordinatorTests
{
    [Fact]
    public async Task SaveAsync_IdentityChangeRotatesKeyClearsProviderIdAndDeletesOldCredential()
    {
        AccountProfile existing = AppTestData.Profile("old@example.test", 9988, "100") with
        {
            RecentServerIds = ["100", "99"],
        };
        var store = new InMemoryProfileStore(existing);
        var vault = new InMemoryCredentialVault();
        vault.Seed(existing.CredentialKey, new CredentialSecret(existing.UserName, "old-secret"));
        var coordinator = new ProfileStorageCoordinator(store, vault);
        var input = new ProfileSaveInput(
            existing,
            "Conta renomeada",
            OasPlatformCatalog.Brazil.Id,
            "new@example.test",
            string.Empty,
            rememberPassword: true);

        ProfileSaveOutcome outcome = await coordinator.SaveAsync(input);

        Assert.True(outcome.WasCredentialPersisted);
        Assert.Null(outcome.Profile.ProviderUserId);
        Assert.Equal("new@example.test", outcome.Profile.UserName);
        Assert.Null(outcome.Profile.LastServerId);
        Assert.Empty(outcome.Profile.RecentServerIds);
        Assert.NotEqual(existing.CredentialKey, outcome.Profile.CredentialKey);
        Assert.NotEqual(CredentialKey.ForProfile(existing.Id), outcome.Profile.CredentialKey);
        CredentialKey.Validate(outcome.Profile.CredentialKey);
        Assert.False(vault.Contains(existing.CredentialKey));
        Assert.False(vault.Contains(outcome.Profile.CredentialKey));
        Assert.Equal(1, vault.DeleteCount);
    }

    [Fact]
    public async Task SaveAsync_SameIdentityReusesCredentialKeyAndSavedCredential()
    {
        AccountProfile existing = AppTestData.Profile("player@example.test", 9988, "100") with
        {
            RecentServerIds = ["100", "99"],
        };
        var store = new InMemoryProfileStore(existing);
        var vault = new InMemoryCredentialVault();
        vault.Seed(existing.CredentialKey, new CredentialSecret(existing.UserName, "old-secret"));
        var coordinator = new ProfileStorageCoordinator(store, vault);
        var input = new ProfileSaveInput(
            existing,
            "Conta renomeada",
            existing.PlatformId,
            existing.UserName,
            string.Empty,
            rememberPassword: true);

        ProfileSaveOutcome outcome = await coordinator.SaveAsync(input);

        Assert.True(outcome.WasCredentialPersisted);
        Assert.Equal(existing.CredentialKey, outcome.Profile.CredentialKey);
        Assert.Equal(existing.ProviderUserId, outcome.Profile.ProviderUserId);
        Assert.Equal(existing.LastServerId, outcome.Profile.LastServerId);
        Assert.Equal(existing.RecentServerIds, outcome.Profile.RecentServerIds);
        Assert.True(vault.Contains(existing.CredentialKey));
        Assert.Equal(0, vault.SetCount);
        Assert.Equal(0, vault.DeleteCount);
        Assert.True(await coordinator.HasSavedCredentialAsync(outcome.Profile));
    }

    [Fact]
    public async Task SaveAsync_RebornToClassicKeepsCredentialAndMaterializesPlatformState()
    {
        const string userName = "player@example.test";
        AccountProfile existing = AppTestData.Profile(userName, 9988, "115") with
        {
            PlatformId = OasPlatformCatalog.RebornTurkish.Id,
            RecentServerIds = ["115", "116"],
            ProviderUserIdsByPlatform = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
            {
                [OasPlatformCatalog.ClassicPortuguese.Id] = 4477,
            },
            RecentServerIdsByPlatform =
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    [OasPlatformCatalog.ClassicPortuguese.Id] = ["100", "101"],
                },
        };
        var store = new InMemoryProfileStore(existing);
        var vault = new InMemoryCredentialVault();
        vault.Seed(existing.CredentialKey, new CredentialSecret(userName, "saved-secret"));
        var coordinator = new ProfileStorageCoordinator(store, vault);
        var input = new ProfileSaveInput(
            existing,
            "Conta Classic",
            OasPlatformCatalog.ClassicPortuguese.Id,
            userName,
            string.Empty,
            rememberPassword: true);

        ProfileSaveOutcome outcome = await coordinator.SaveAsync(input);
        AccountProfile stored = Assert.Single(store.Values);

        Assert.Equal(existing.Id, outcome.Profile.Id);
        Assert.Equal(existing.CredentialKey, outcome.Profile.CredentialKey);
        Assert.Equal(OasPlatformCatalog.ClassicPortuguese.Id, outcome.Profile.PlatformId);
        Assert.Equal(4477, outcome.Profile.ProviderUserId);
        Assert.Equal("100", outcome.Profile.LastServerId);
        Assert.Equal(["100", "101"], outcome.Profile.RecentServerIds);
        Assert.Equal(9988, outcome.Profile.GetProviderUserId(OasPlatformCatalog.RebornTurkish.Id));
        Assert.Equal(
            ["115", "116"],
            outcome.Profile.GetRecentServerIds(OasPlatformCatalog.RebornTurkish.Id));
        Assert.Equal(4477, outcome.Profile.GetProviderUserId(OasPlatformCatalog.ClassicPortuguese.Id));
        Assert.Equal(
            ["100", "101"],
            outcome.Profile.GetRecentServerIds(OasPlatformCatalog.ClassicPortuguese.Id));
        Assert.Equal(outcome.Profile, stored);
        Assert.True(vault.Contains(existing.CredentialKey));
        Assert.Equal(0, vault.SetCount);
        Assert.Equal(0, vault.DeleteCount);
        Assert.True(await coordinator.HasSavedCredentialAsync(outcome.Profile));
    }

    [Fact]
    public async Task SaveAsync_OasToSevenWanRotatesCredentialAndClearsOasPlatformState()
    {
        const string userName = "player@example.test";
        AccountProfile existing = AppTestData.Profile(userName, 9988, "115") with
        {
            PlatformId = OasPlatformCatalog.RebornTurkish.Id,
            RecentServerIds = ["115", "116"],
        };
        var store = new InMemoryProfileStore(existing);
        var vault = new InMemoryCredentialVault();
        vault.Seed(existing.CredentialKey, new CredentialSecret(userName, "saved-secret"));
        var coordinator = new ProfileStorageCoordinator(store, vault);
        var input = new ProfileSaveInput(
            existing,
            "Conta 7wan",
            SevenWanPlatformCatalog.All[0].Id,
            userName,
            string.Empty,
            rememberPassword: true);

        ProfileSaveOutcome outcome = await coordinator.SaveAsync(input);

        Assert.Equal(existing.Id, outcome.Profile.Id);
        Assert.NotEqual(existing.CredentialKey, outcome.Profile.CredentialKey);
        Assert.Null(outcome.Profile.ProviderUserId);
        Assert.Null(outcome.Profile.LastServerId);
        Assert.Empty(outcome.Profile.RecentServerIds);
        Assert.Empty(outcome.Profile.ProviderUserIdsByPlatform);
        Assert.Empty(outcome.Profile.RecentServerIdsByPlatform);
        Assert.False(vault.Contains(existing.CredentialKey));
        Assert.False(vault.Contains(outcome.Profile.CredentialKey));
        Assert.Equal(1, vault.DeleteCount);
        Assert.False(await coordinator.HasSavedCredentialAsync(outcome.Profile));
    }

    [Fact]
    public async Task SaveAsync_OldCredentialDeleteFailureCannotReExposeItToChangedIdentity()
    {
        const string userName = "player@example.test";
        AccountProfile existing = AppTestData.Profile(userName, 9988, "100");
        var store = new InMemoryProfileStore(existing);
        var vault = new DeleteFailingCredentialVault(existing.CredentialKey);
        vault.Seed(existing.CredentialKey, new CredentialSecret(userName, "old-secret"));
        var coordinator = new ProfileStorageCoordinator(store, vault);
        var input = new ProfileSaveInput(
            existing,
            "Conta 7wan",
            SevenWanPlatformCatalog.All[0].Id,
            userName,
            string.Empty,
            rememberPassword: true);

        ProfileSaveOutcome outcome = await coordinator.SaveAsync(input);
        AccountProfile stored = Assert.Single(store.Values);

        Assert.False(outcome.WasCredentialPersisted);
        Assert.Equal(outcome.Profile, stored);
        Assert.NotEqual(existing.CredentialKey, stored.CredentialKey);
        Assert.True(vault.Contains(existing.CredentialKey));
        Assert.False(vault.Contains(stored.CredentialKey));
        Assert.False(await coordinator.HasSavedCredentialAsync(stored));

        var sameIdentityRetry = new ProfileSaveInput(
            stored,
            stored.DisplayName,
            stored.PlatformId,
            stored.UserName,
            string.Empty,
            rememberPassword: true);
        ProfileSaveOutcome retryOutcome = await coordinator.SaveAsync(sameIdentityRetry);

        Assert.True(retryOutcome.WasCredentialPersisted);
        Assert.Equal(stored.CredentialKey, retryOutcome.Profile.CredentialKey);
        Assert.False(await coordinator.HasSavedCredentialAsync(retryOutcome.Profile));
    }

    [Fact]
    public async Task SaveAsync_NewProfileStoresPasswordOnlyWhenOptedIn()
    {
        const string password = "save-this-secret";
        var store = new InMemoryProfileStore();
        var vault = new InMemoryCredentialVault();
        var coordinator = new ProfileStorageCoordinator(store, vault);
        var input = new ProfileSaveInput(
            null,
            "Principal",
            OasPlatformCatalog.Brazil.Id,
            "player@example.test",
            password,
            rememberPassword: true);

        ProfileSaveOutcome outcome = await coordinator.SaveAsync(input);

        Assert.True(outcome.WasCredentialPersisted);
        Assert.True(vault.Contains(outcome.Profile.CredentialKey));
        Assert.Single(store.Values);
        Assert.Null(outcome.Profile.LastServerId);
        Assert.DoesNotContain(password, input.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("player@example.test", outcome.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveAsync_VaultFailureStillReportsPersistedNonSecretProfile()
    {
        var store = new InMemoryProfileStore();
        var vault = new InMemoryCredentialVault { ThrowOnSet = true };
        var coordinator = new ProfileStorageCoordinator(store, vault);
        var input = new ProfileSaveInput(
            null,
            "Principal",
            OasPlatformCatalog.Brazil.Id,
            "player@example.test",
            "secret",
            rememberPassword: true);

        ProfileSaveOutcome outcome = await coordinator.SaveAsync(input);

        Assert.False(outcome.WasCredentialPersisted);
        Assert.Equal(outcome.Profile, Assert.Single(store.Values));
        Assert.False(vault.Contains(outcome.Profile.CredentialKey));
    }

    private sealed class DeleteFailingCredentialVault(string blockedKey) : ICredentialVault
    {
        private readonly Dictionary<string, CredentialSecret> _credentials =
            new(StringComparer.Ordinal);

        public void Seed(string key, CredentialSecret credential) => _credentials[key] = credential;

        public bool Contains(string key) => _credentials.ContainsKey(key);

        public Task<CredentialSecret?> GetAsync(
            string credentialKey,
            CancellationToken cancellationToken = default)
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
            _credentials[credentialKey] = credential;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(
            string credentialKey,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.Equals(credentialKey, blockedKey, StringComparison.Ordinal))
            {
                throw new IOException("credential delete unavailable");
            }

            _credentials.Remove(credentialKey);
            return Task.CompletedTask;
        }
    }
}

using LegendLauncher.App.Services;
using LegendLauncher.App.Localization;
using LegendLauncher.Tests.Infrastructure;

namespace LegendLauncher.Tests.App;

public sealed class LauncherSettingsServiceTests
{
    [Fact]
    public async Task MissingSettingsUseMutedFourPaneDefaults()
    {
        using var directory = new TemporaryDirectory();
        var service = new LauncherSettingsService(directory.Combine("settings.json"));

        LauncherSettingsSnapshot settings = await service.LoadAsync();

        Assert.True(settings.IsGameMuted);
        Assert.Equal(GameLayoutMode.GridFour, settings.LayoutMode);
        Assert.Null(settings.LastSelectedProfileId);
        Assert.Equal(LocalizationService.DefaultLanguageCode, settings.LanguageCode);
        Assert.Null(settings.LastDonationPromptUtc);
    }

    [Fact]
    public async Task IndependentUpdatesPreserveProfileAndGamePreferences()
    {
        using var directory = new TemporaryDirectory();
        string path = directory.Combine("settings.json");
        var service = new LauncherSettingsService(path);
        Guid profileId = Guid.NewGuid();
        var donationShownAt = new DateTimeOffset(
            2026,
            7,
            15,
            2,
            30,
            0,
            TimeSpan.FromHours(-3));

        await service.SaveLastSelectedProfileAsync(profileId);
        await service.SaveGamePreferencesAsync(false, GameLayoutMode.SplitTwo);
        await service.SaveLanguageAsync("es-MX");
        await service.SaveDonationPromptShownAsync(donationShownAt);

        var reloaded = new LauncherSettingsService(path);
        LauncherSettingsSnapshot settings = await reloaded.LoadAsync();
        Assert.False(settings.IsGameMuted);
        Assert.Equal(GameLayoutMode.SplitTwo, settings.LayoutMode);
        Assert.Equal(profileId, settings.LastSelectedProfileId);
        Assert.Equal(LocalizationService.SpanishLanguageCode, settings.LanguageCode);
        Assert.Equal(donationShownAt.ToUniversalTime(), settings.LastDonationPromptUtc);
    }

    [Fact]
    public async Task CorruptSettingsFallBackAndAreReplacedOnNextUpdate()
    {
        using var directory = new TemporaryDirectory();
        string path = directory.Combine("settings.json");
        await File.WriteAllTextAsync(path, "{broken");
        var service = new LauncherSettingsService(path);

        LauncherSettingsSnapshot fallback = await service.LoadAsync();
        Assert.Equal(LauncherSettingsSnapshot.Default, fallback);

        await service.SaveGamePreferencesAsync(false, GameLayoutMode.Single);
        LauncherSettingsSnapshot repaired = await service.LoadAsync();
        Assert.False(repaired.IsGameMuted);
        Assert.Equal(GameLayoutMode.Single, repaired.LayoutMode);
        Assert.Equal(LocalizationService.DefaultLanguageCode, repaired.LanguageCode);
        Assert.Null(repaired.LastDonationPromptUtc);
    }

    [Fact]
    public async Task LegacyDocumentWithoutLanguageMigratesToPortugueseDefault()
    {
        using var directory = new TemporaryDirectory();
        string path = directory.Combine("settings.json");
        Guid profileId = Guid.NewGuid();
        await File.WriteAllTextAsync(
            path,
            $$"""
            {
              "isGameMuted": false,
              "layoutMode": 2,
              "lastSelectedProfileId": "{{profileId}}"
            }
            """);

        LauncherSettingsSnapshot settings = await new LauncherSettingsService(path).LoadAsync();

        Assert.False(settings.IsGameMuted);
        Assert.Equal(GameLayoutMode.SplitTwo, settings.LayoutMode);
        Assert.Equal(profileId, settings.LastSelectedProfileId);
        Assert.Equal(LocalizationService.DefaultLanguageCode, settings.LanguageCode);
        Assert.Null(settings.LastDonationPromptUtc);
    }

    [Theory]
    [InlineData("en-GB", "en-US")]
    [InlineData("es-AR", "es-ES")]
    [InlineData("unsupported", "pt-BR")]
    public async Task SaveLanguageNormalizesAndPreservesOtherPreferences(
        string requested,
        string expected)
    {
        using var directory = new TemporaryDirectory();
        string path = directory.Combine("settings.json");
        var service = new LauncherSettingsService(path);
        Guid profileId = Guid.NewGuid();
        DateTimeOffset donationShownAt = new(2026, 7, 14, 20, 0, 0, TimeSpan.Zero);
        await service.SaveLastSelectedProfileAsync(profileId);
        await service.SaveGamePreferencesAsync(false, GameLayoutMode.Single);
        await service.SaveDonationPromptShownAsync(donationShownAt);

        await service.SaveLanguageAsync(requested);
        LauncherSettingsSnapshot settings = await service.LoadAsync();

        Assert.Equal(expected, settings.LanguageCode);
        Assert.False(settings.IsGameMuted);
        Assert.Equal(GameLayoutMode.Single, settings.LayoutMode);
        Assert.Equal(profileId, settings.LastSelectedProfileId);
        Assert.Equal(donationShownAt, settings.LastDonationPromptUtc);
    }
}

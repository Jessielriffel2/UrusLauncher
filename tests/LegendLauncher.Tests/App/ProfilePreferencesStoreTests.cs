using LegendLauncher.App.MacroAssistant;
using LegendLauncher.App.Services;
using LegendLauncher.Tests.Infrastructure;

namespace LegendLauncher.Tests.App;

public sealed class ProfilePreferencesStoreTests
{
    [Fact]
    public async Task SaveAndLoad_PreservesSettingsPerProfileAndMode()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var store = new ProfilePreferencesStore(temporaryDirectory.Combine("profile-preferences.json"));
        Guid firstProfile = Guid.NewGuid();
        Guid secondProfile = Guid.NewGuid();
        MacroProfilePreferences gems = MacroProfilePreferences.Default with
        {
            TargetX = 0.23,
            TargetY = 0.67,
            AnalysisWidth = 0.41,
            AnalysisHeight = 0.29,
            Speed = 2.5,
        };
        MacroProfilePreferences clicks = MacroProfilePreferences.Default with
        {
            ClickCount = 12,
            ClickIntervalSeconds = 0.25,
            Speed = 1.75,
            UseLargeFrame = true,
        };

        await store.SaveAsync(firstProfile, MacroMode.Gems, gems);
        await store.SaveAsync(firstProfile, MacroMode.Clicks, clicks);
        await store.SaveAsync(secondProfile, MacroMode.Cosmo, MacroProfilePreferences.Default with { Speed = 0.5 });

        ProfileMacroModes first = await store.LoadAsync(firstProfile);
        ProfileMacroModes second = await store.LoadAsync(secondProfile);

        Assert.Equal(0.23, first.Get(MacroMode.Gems).TargetX, 3);
        Assert.Equal(2.5, first.Get(MacroMode.Gems).Speed, 3);
        Assert.Equal(12, first.Get(MacroMode.Clicks).ClickCount);
        Assert.Equal(0.25, first.Get(MacroMode.Clicks).ClickIntervalSeconds, 3);
        Assert.Equal(0.5, second.Get(MacroMode.Cosmo).Speed, 3);
    }

    [Fact]
    public async Task DeleteAsync_RemovesOnlyTheRequestedProfile()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var store = new ProfilePreferencesStore(temporaryDirectory.Combine("profile-preferences.json"));
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        await store.SaveAsync(first, MacroMode.Gems, MacroProfilePreferences.Default);
        await store.SaveAsync(second, MacroMode.Gems, MacroProfilePreferences.Default);

        await store.DeleteAsync(first);

        Assert.Equal(MacroProfilePreferences.Default, (await store.LoadAsync(first)).Get(MacroMode.Gems));
        Assert.Equal(MacroProfilePreferences.Default, (await store.LoadAsync(second)).Get(MacroMode.Gems));
    }

    [Fact]
    public async Task CorruptJson_IsRecoveredAsDefaults()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        string filePath = temporaryDirectory.Combine("profile-preferences.json");
        await File.WriteAllTextAsync(filePath, "{ definitely not json");
        var store = new ProfilePreferencesStore(filePath);

        ProfileMacroModes modes = await store.LoadAsync(Guid.NewGuid());

        Assert.Equal(MacroProfilePreferences.Default, modes.Get(MacroMode.Gems));
    }

    [Fact]
    public void Normalized_ClampsUnsafeValues()
    {
        MacroProfilePreferences normalized = new MacroProfilePreferences(
            2,
            -1,
            0.99,
            0.99,
            0,
            0,
            false,
            99,
            -4,
            0).Normalized();

        Assert.Equal(1, normalized.TargetX);
        Assert.Equal(0, normalized.TargetY);
        Assert.InRange(normalized.AnalysisX, 0, 1 - normalized.AnalysisWidth);
        Assert.InRange(normalized.AnalysisY, 0, 1 - normalized.AnalysisHeight);
        Assert.Equal(4, normalized.Speed);
        Assert.Equal(0, normalized.ClickCount);
        Assert.Equal(0.01, normalized.ClickIntervalSeconds, 3);
    }
}

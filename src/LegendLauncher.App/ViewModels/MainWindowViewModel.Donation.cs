using System.IO;
using LegendLauncher.App.Services;

namespace LegendLauncher.App.ViewModels;

internal sealed partial class MainWindowViewModel
{
    private static readonly TimeSpan DonationPromptInterval = TimeSpan.FromHours(5);

    private bool _isDonationPromptVisible;
    private bool _donationPromptWasEvaluated;

    public bool IsDonationPromptVisible
    {
        get => _isDonationPromptVisible;
        private set => SetProperty(ref _isDonationPromptVisible, value);
    }

    public AsyncRelayCommand OpenDonationPromptCommand { get; }

    public RelayCommand CloseDonationPromptCommand { get; }

    private async Task EvaluateDonationPromptOnOpeningAsync(
        LauncherSettingsSnapshot settings)
    {
        if (_donationPromptWasEvaluated)
        {
            return;
        }

        _donationPromptWasEvaluated = true;
        DateTimeOffset now = _timeProvider.GetUtcNow().ToUniversalTime();
        if (!ShouldShowDonationPrompt(settings.LastDonationPromptUtc, now))
        {
            return;
        }

        IsDonationPromptVisible = true;
        await TryPersistDonationPromptShownAsync(now).ConfigureAwait(true);
    }

    private async Task OpenDonationPromptAsync()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow().ToUniversalTime();
        IsDonationPromptVisible = true;
        await TryPersistDonationPromptShownAsync(now).ConfigureAwait(true);
    }

    private void CloseDonationPrompt() => IsDonationPromptVisible = false;

    private async Task TryPersistDonationPromptShownAsync(DateTimeOffset shownAtUtc)
    {
        try
        {
            await _settingsService
                .SaveDonationPromptShownAsync(shownAtUtc)
                .ConfigureAwait(true);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            // The prompt remains usable even if its display time cannot be persisted.
        }
    }

    private static bool ShouldShowDonationPrompt(
        DateTimeOffset? lastShownUtc,
        DateTimeOffset nowUtc)
    {
        if (lastShownUtc is null)
        {
            return true;
        }

        DateTimeOffset normalizedLastShown = lastShownUtc.Value.ToUniversalTime();
        return normalizedLastShown > nowUtc ||
            nowUtc - normalizedLastShown >= DonationPromptInterval;
    }
}

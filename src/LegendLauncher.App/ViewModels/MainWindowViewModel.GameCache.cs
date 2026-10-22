using System.ComponentModel;
using LegendLauncher.App.Services;
using LegendLauncher.Providers.SwordOnline;
#if LOREBORN_LOCAL
using LegendLauncher.Providers.Local;
#endif

namespace LegendLauncher.App.ViewModels;

/// <summary>
/// "Clear game cache" link under the version text: removes the SwordOnline game files from the Windows cache used by the
/// Flash ActiveX (<see cref="GameCacheCleaner"/>), so the next game opening downloads them again. The first click only arms
/// the action (the link asks for a second click); it is refused while a game window is open, because that session keeps
/// using the files it already loaded.
/// </summary>
internal sealed partial class MainWindowViewModel
{
    private RelayCommand? _clearGameCacheCommand;
    private bool _isClearGameCacheArmed;
    private bool _isClearingGameCache;

    public RelayCommand ClearGameCacheCommand =>
        _clearGameCacheCommand ??= new RelayCommand(ClearGameCache, () => !_isClearingGameCache);

    public string ClearGameCacheButtonText =>
        _localization.Get(_isClearingGameCache
            ? "GameCache_Clearing"
            : _isClearGameCacheArmed ? "GameCache_Confirm" : "GameCache_Button");

    /// <summary>Removes the cached game entries (replaceable in tests).</summary>
    internal Func<Func<Uri, bool>, GameCacheCleanResult> GameCacheClearer { get; set; } = GameCacheCleaner.Clear;

    internal static bool IsGameCacheUrl(Uri uri)
    {
        string host = uri.Host;
        if (string.Equals(host, SwordOnlinePlatformCatalog.Host, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith("." + SwordOnlinePlatformCatalog.Host, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

#if LOREBORN_LOCAL
        return string.Equals(host, LocalPlatformCatalog.Host, StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase);
#else
        return false;
#endif
    }

    private async void ClearGameCache()
    {
        if (_isClearingGameCache)
        {
            return;
        }

        if (Workspace.HasSessions)
        {
            SetClearGameCacheState(armed: false, clearing: false);
            SetStatusMessage("GameCache_CloseGamesFirst");
            return;
        }

        if (!_isClearGameCacheArmed)
        {
            SetClearGameCacheState(armed: true, clearing: false);
            return;
        }

        SetClearGameCacheState(armed: false, clearing: true);
        try
        {
            GameCacheCleanResult result = await Task.Run(() => GameCacheClearer(IsGameCacheUrl)).ConfigureAwait(true);
            double megabytes = Math.Round(result.Bytes / 1048576.0, 1);
            if (result.Failed > 0)
            {
                SetStatusMessage("GameCache_ClearedPartial", result.Deleted, megabytes, result.Failed);
            }
            else
            {
                SetStatusMessage("GameCache_Cleared", result.Deleted, megabytes);
            }
        }
        catch (Exception exception) when (exception is Win32Exception or DllNotFoundException or EntryPointNotFoundException)
        {
            LogFailure("game_cache.clear_failed", "The game files could not be removed from the Windows cache.", exception);
            SetStatusMessage("GameCache_Failed");
        }
        finally
        {
            SetClearGameCacheState(armed: false, clearing: false);
        }
    }

    private void SetClearGameCacheState(bool armed, bool clearing)
    {
        _isClearGameCacheArmed = armed;
        _isClearingGameCache = clearing;
        OnPropertyChanged(nameof(ClearGameCacheButtonText));
        ClearGameCacheCommand.NotifyCanExecuteChanged();
    }
}

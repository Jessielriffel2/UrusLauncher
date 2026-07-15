using System.Windows;
using System.IO;
using LegendLauncher.App.Localization;
using LegendLauncher.App.Services;
using LegendLauncher.Infrastructure.Paths;

namespace LegendLauncher.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs eventArgs)
    {
        LocalizationService localization = LocalizationService.Current;
        try
        {
            var paths = new AppPaths();
            paths.EnsureDirectories();
            var settings = new LauncherSettingsService(paths.SettingsFile);
            LauncherSettingsSnapshot snapshot = settings.LoadAsync().GetAwaiter().GetResult();
            localization.SetLanguage(snapshot.LanguageCode);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            localization.SetLanguage(LocalizationService.DefaultLanguageCode);
        }

        localization.EnableThreadCultureUpdates();
        base.OnStartup(eventArgs);
    }
}

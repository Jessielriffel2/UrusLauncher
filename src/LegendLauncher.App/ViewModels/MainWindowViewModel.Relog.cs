using LegendLauncher.App.Localization;
using LegendLauncher.App.Services;
using LegendLauncher.Core.Models;

namespace LegendLauncher.App.ViewModels;

internal sealed partial class MainWindowViewModel
{
    private async void WorkspaceOnRelogRequested(object? sender, GameSessionViewModel session)
    {
        try
        {
            await RelogSessionAsync(session).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Relog cancellation must never crash the workspace.
        }
    }

    internal async Task RelogSessionAsync(GameSessionViewModel session)
    {
        if (_disposed || IsLaunching || !session.IsRunning)
        {
            return;
        }

        AccountProfile profile = session.Profile;
        PlatformDefinition platform = session.Platform;
        GameServer server = session.Server;

        Workspace.RequestClose(session);

        var relogInput = new SessionLaunchInput(
            profile,
            platform,
            server,
            profile.DisplayName,
            profile.UserName,
            typedPassword: string.Empty,
            rememberPassword: true);

        SetCatalogStatus("Auth_Authenticating");
        CatalogStatusBrush = WarningBrush;
        SetStatusMessage("Auth_Connecting");
        await LaunchAsync(relogInput).ConfigureAwait(true);
    }
}

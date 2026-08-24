using LegendLauncher.App.Services;
using LegendLauncher.Core.Models;

namespace LegendLauncher.App.ViewModels;

internal sealed partial class MainWindowViewModel
{
    internal async Task StartGameAsync()
    {
        if (SelectedProfile?.Model is { } activeProfile &&
            SelectedServer?.Model is { } activeServer &&
            Workspace.TryActivateSession(
                activeProfile.Id,
                SelectedPlatform.Id,
                activeServer.Id))
        {
            IsWorkspaceVisible = true;
            SetCatalogStatus("Session_AlreadyActive");
            CatalogStatusBrush = OnlineBrush;
            SetStatusMessage("Session_AlreadyActiveMessage");
            return;
        }

        if (!CanStartGame || SelectedServer is null)
        {
            if (!_runtimeProbe.IsUsable)
            {
                LogFailure(
                    "runtime.unavailable",
                    "The Flash compatibility runtime is not usable.",
                    exception: null,
                    ("runtimeDirectory", _runtimeProbe.RuntimeDirectory),
                    ("missingComponents", string.Join(",", _runtimeProbe.MissingComponents)));
            }

            SetStatusMessage(_runtimeProbe.IsUsable
                ? "Session_ChooseServerAndLogin"
                : "Runtime_NotAvailable");
            CatalogStatusBrush = WarningBrush;
            return;
        }

        ProfileItemViewModel? requestedProfile = SelectedProfile;
        PlatformDefinition launchedPlatform = SelectedPlatform.Model;
        GameServer launchedServer = SelectedServer.Model;
        var launchInput = new SessionLaunchInput(
            requestedProfile?.Model,
            launchedPlatform,
            launchedServer,
            ProfileLabel.Trim(),
            LoginHint.Trim(),
            PendingPassword,
            RememberPassword);

        await LaunchAsync(launchInput);
    }

    private async Task LaunchAsync(SessionLaunchInput launchInput)
    {
        _launchCancellation?.Cancel();
        _launchCancellation?.Dispose();
        var launchCancellation = new CancellationTokenSource();
        _launchCancellation = launchCancellation;
        CancellationToken cancellationToken = launchCancellation.Token;

        IsLaunching = true;
        SetCatalogStatus("Auth_Authenticating");
        CatalogStatusBrush = WarningBrush;
        SetStatusMessage("Auth_Connecting");

        GameSession? pendingGameSession = null;
        bool gameSessionAdopted = false;
        try
        {
            SessionLaunchOutcome outcome = await _sessionLauncher
                .LaunchAsync(launchInput, cancellationToken)
                .ConfigureAwait(true);
            if (outcome.State == SessionLaunchState.CredentialRequired)
            {
                LogFailure(
                    "session.credential_required",
                    "A password is required before authentication can continue.",
                    exception: null,
                    ("platformId", launchInput.Platform.Id),
                    ("serverId", launchInput.Server.Id),
                    ("serverName", launchInput.Server.Name),
                    ("login", launchInput.Login),
                    ("profileId", launchInput.Profile?.Id.ToString()));
                SetCatalogStatus("Auth_PasswordRequired");
                CatalogStatusBrush = WarningBrush;
                SetStatusMessage("Auth_PasswordRequiredMessage");
                return;
            }

            if (outcome.State == SessionLaunchState.AuthenticationRejected)
            {
                LogFailure(
                    "session.authentication_rejected",
                    "Authentication was rejected before the game could open.",
                    exception: null,
                    ("platformId", launchInput.Platform.Id),
                    ("serverId", launchInput.Server.Id),
                    ("serverName", launchInput.Server.Name),
                    ("login", launchInput.Login),
                    ("profileId", launchInput.Profile?.Id.ToString()),
                    ("credentialSource", outcome.CredentialSource.ToString()),
                    ("errorCode", outcome.ErrorCode),
                    ("errorMessage", outcome.ErrorMessage),
                    ("failureDiagnostic", outcome.FailureDiagnostic?.ToString()));
                CatalogStatusBrush = ErrorBrush;
                IsProfileEditorVisible = true;
                if (outcome.CredentialSource == SessionCredentialSource.Stored &&
                    IsCredentialRejection(outcome.ErrorCode))
                {
                    HasSavedCredential = false;
                    IsProfileEditorVisible = true;
                    SetCatalogStatus("Auth_RetypePassword");
                    SetStatusMessage("Auth_SavedPasswordRejected");
                    return;
                }

                SetCatalogStatus("Auth_LoginNotConfirmed");
                SetStatusMessage(BuildAuthenticationFailureKey(outcome.ErrorCode),
                    string.IsNullOrWhiteSpace(outcome.ErrorCode) ? [] : [outcome.ErrorCode]);
                return;
            }

            GameSession gameSession = outcome.GameSession ??
                throw new InvalidOperationException("The session launcher returned no game process.");
            pendingGameSession = gameSession;
            AccountProfile launchedProfile = outcome.EffectiveProfile ??
                throw new InvalidOperationException("The session launcher returned no profile snapshot.");
            await ApplyLaunchedProfileAsync(launchInput.Profile, launchedProfile, outcome.WasProfilePersisted)
                .ConfigureAwait(true);

            PendingPassword = string.Empty;
            Workspace.AddSession(
                launchedProfile,
                launchInput.Platform,
                launchInput.Server,
                gameSession);
            gameSessionAdopted = true;
            NotifyGameReadiness();
            IsWorkspaceVisible = true;
            ShowWorkspaceCommand.NotifyCanExecuteChanged();
            SetCatalogStatus("Game_Started");
            CatalogStatusBrush = OnlineBrush;
            SetStatusMessage(
                !outcome.WasProfilePersisted
                    ? "Game_ProfileUpdateFailed"
                    : !outcome.WasCredentialPersisted
                        ? "Game_CredentialSaveFailed"
                        : "Game_Opened",
                gameSession.ProcessId);
            if (!outcome.WasProfilePersisted)
            {
                LogFailure(
                    "session.profile_persist_failed",
                    "The game opened, but the profile could not be saved.",
                    exception: null,
                    ("profileId", launchedProfile.Id.ToString()),
                    ("platformId", launchInput.Platform.Id),
                    ("serverId", launchInput.Server.Id));
            }
            else if (!outcome.WasCredentialPersisted)
            {
                LogFailure(
                    "session.credential_persist_failed",
                    "The game opened, but the saved password could not be updated.",
                    exception: null,
                    ("profileId", launchedProfile.Id.ToString()),
                    ("platformId", launchInput.Platform.Id),
                    ("serverId", launchInput.Server.Id));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            LogFailure(
                "session.cancelled",
                "Game launch was cancelled before the session opened.",
                exception: null,
                ("platformId", launchInput.Platform.Id),
                ("serverId", launchInput.Server.Id),
                ("login", launchInput.Login));
            SetCatalogStatus("Game_Cancelled");
            CatalogStatusBrush = MutedBrush;
            SetStatusMessage("Game_CancelledMessage");
        }
        catch (Exception exception)
        {
            LogFailure(
                "session.start_failed",
                "Game launch failed after authentication or process start.",
                exception,
                ("platformId", launchInput.Platform.Id),
                ("serverId", launchInput.Server.Id),
                ("serverName", launchInput.Server.Name),
                ("login", launchInput.Login),
                ("profileId", launchInput.Profile?.Id.ToString()),
                ("runtimeDirectory", _runtimeProbe.RuntimeDirectory));
            SetCatalogStatus("Game_StartFailed");
            CatalogStatusBrush = ErrorBrush;
            SetStatusMessage("Game_StartFailedMessage");
        }
        finally
        {
            if (pendingGameSession is not null &&
                !gameSessionAdopted &&
                !Workspace.Sessions.Any(session =>
                    session.ProcessId == pendingGameSession.ProcessId &&
                    session.NativeWindowHandle == pendingGameSession.NativeWindowHandle))
            {
                _terminateUnadoptedProcess(pendingGameSession.ProcessId);
            }

            if (ReferenceEquals(_launchCancellation, launchCancellation))
            {
                _launchCancellation = null;
            }

            launchCancellation.Dispose();
            IsLaunching = false;
        }
    }
}

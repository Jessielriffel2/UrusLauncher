using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using LegendLauncher.App.Localization;
using LegendLauncher.App.Services;
using LegendLauncher.App.Updates;
using LegendLauncher.Core.Contracts;
using LegendLauncher.Core.Models;
using LegendLauncher.Infrastructure.Runtime;
using LegendLauncher.Infrastructure.Security;

namespace LegendLauncher.App.ViewModels;

internal sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private static readonly Brush OnlineBrush = CreateBrush(0x58, 0xD6, 0x8D);
    private static readonly Brush WarningBrush = CreateBrush(0xF5, 0xB9, 0x5A);
    private static readonly Brush ErrorBrush = CreateBrush(0xFF, 0x6B, 0x7A);
    private static readonly Brush MutedBrush = CreateBrush(0x70, 0x80, 0x97);

    private readonly IServerDirectory _serverDirectory;
    private readonly ProfileStorageCoordinator _profileStorage;
    private readonly SessionLaunchCoordinator _sessionLauncher;
    private readonly LauncherSettingsService _settingsService;
    private readonly LegacyRuntimeProbeResult _runtimeProbe;
    private readonly Action<int> _terminateUnadoptedProcess;
    private readonly TimeProvider _timeProvider;
    private readonly List<ServerRowViewModel> _allServers = [];
    private CancellationTokenSource? _catalogCancellation;
    private CancellationTokenSource? _launchCancellation;
    private PlatformItemViewModel _selectedPlatform;
    private ProfileItemViewModel? _selectedProfile;
    private ServerRowViewModel? _selectedServer;
    private IReadOnlyList<ServerRowViewModel> _recentServers = [];
    private IReadOnlyList<ServerRowViewModel> _visibleServers = [];
    private string _searchText = string.Empty;
    private string _profileLabel = string.Empty;
    private string _loginHint = string.Empty;
    private string _pendingPassword = string.Empty;
    private string? _pendingServerId;
    private string? _serverIdBeforeFilter;
    private bool _rememberPassword;
    private bool _isLoading;
    private bool _isLaunching;
    private bool _isCredentialStateLoading;
    private bool _hasSavedCredential;
    private bool _isWorkspaceVisible;
    private bool _isProfileEditorVisible;
    private bool _suppressProfileCatalogReload;
    private Brush _catalogStatusBrush = MutedBrush;
    private bool _disposed;

    public MainWindowViewModel(
        IServerDirectory serverDirectory,
        ProfileStorageCoordinator profileStorage,
        SessionLaunchCoordinator sessionLauncher,
        LegacyRuntimeProbeResult runtimeProbe,
        IEnumerable<PlatformDefinition> platforms,
        TimeProvider? timeProvider = null,
        LauncherSettingsService? settingsService = null,
        GameWorkspaceViewModel? workspace = null,
        Action<int>? terminateUnadoptedProcess = null,
        LocalizationService? localization = null,
        ILauncherUpdateService? updateService = null,
        Version? currentVersion = null)
    {
        _serverDirectory = serverDirectory;
        _profileStorage = profileStorage;
        _sessionLauncher = sessionLauncher;
        _runtimeProbe = runtimeProbe;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _settingsService = settingsService ?? new LauncherSettingsService();
        _localization = localization ?? LocalizationService.Current;
        _terminateUnadoptedProcess = terminateUnadoptedProcess ??
            SessionLaunchCoordinator.TryTerminateProcess;
        Workspace = workspace ?? new GameWorkspaceViewModel(
            new GameAudioService(static (_, _) => { }, TimeSpan.FromHours(1)),
            _settingsService,
            static (_, _) => null,
            _localization);
        Workspace.SessionRemoved += WorkspaceOnSessionRemoved;
        InitializeLocalization();
        InitializeUpdater(updateService, currentVersion);

        OpenDonationPromptCommand = new AsyncRelayCommand(OpenDonationPromptAsync);
        CloseDonationPromptCommand = new RelayCommand(CloseDonationPrompt);

        var platformItems = platforms.Select(static platform => new PlatformItemViewModel(platform)).ToArray();
        if (platformItems.Length == 0)
        {
            throw new ArgumentException("At least one platform is required.", nameof(platforms));
        }

        Platforms = Array.AsReadOnly(platformItems);
        _selectedPlatform = platformItems[0];
        Profiles = [];

        NewProfileCommand = new RelayCommand(NewProfile, () => !IsLaunching);
        AddAccountCommand = new RelayCommand(AddAccount, () => !IsLaunching);
        EditProfileCommand = new RelayCommand(EditProfile, () => SelectedProfile is not null && !IsLaunching);
        CancelProfileEditCommand = new RelayCommand(CancelProfileEdit, () => !IsLaunching);
        ShowLauncherCommand = new RelayCommand(() => IsWorkspaceVisible = false);
        ShowWorkspaceCommand = new RelayCommand(
            () => IsWorkspaceVisible = true,
            () => Workspace.HasSessions);
        RefreshServersCommand = new AsyncRelayCommand(
            () => LoadServersAsync(forceRefresh: true),
            () => !IsLoading && !IsLaunching);
        SaveProfileCommand = new AsyncRelayCommand(
            SaveProfileAsync,
            () => !IsLoading && !IsLaunching);
        DeleteProfileCommand = new AsyncRelayCommand(
            DeleteProfileAsync,
            () => SelectedProfile is not null && !IsLoading && !IsLaunching);
        SelectRecentServerCommand = new RelayCommand<ServerRowViewModel>(
            SelectRecentServer,
            server =>
                server?.CanLaunch == true &&
                !IsLoading &&
                !IsLaunching &&
                RecentServers.Contains(server));
        PlayCommand = new AsyncRelayCommand(StartGameAsync, () => CanStartGame);
    }

    public IReadOnlyList<PlatformItemViewModel> Platforms { get; }

    public ObservableCollection<ProfileItemViewModel> Profiles { get; }

    public GameWorkspaceViewModel Workspace { get; }

    public RelayCommand NewProfileCommand { get; }

    public RelayCommand AddAccountCommand { get; }

    public RelayCommand EditProfileCommand { get; }

    public RelayCommand CancelProfileEditCommand { get; }

    public RelayCommand ShowLauncherCommand { get; }

    public RelayCommand ShowWorkspaceCommand { get; }

    public AsyncRelayCommand RefreshServersCommand { get; }

    public AsyncRelayCommand SaveProfileCommand { get; }

    public AsyncRelayCommand DeleteProfileCommand { get; }

    public RelayCommand<ServerRowViewModel> SelectRecentServerCommand { get; }

    public AsyncRelayCommand PlayCommand { get; }

    public PlatformItemViewModel SelectedPlatform
    {
        get => _selectedPlatform;
        set
        {
            if (value is null || !SetProperty(ref _selectedPlatform, value))
            {
                return;
            }

            _serverIdBeforeFilter = null;
            RecentServers = [];
            _pendingServerId = ServerCatalogPresentation.ResolveLastPlayedServerId(
                SelectedProfile?.Model,
                value.Id);
            if (SelectedProfile?.Model is { } selectedProfile &&
                IsSelectedProfileIdentity(selectedProfile))
            {
                BeginCredentialStateLoad(selectedProfile);
            }
            else
            {
                HasSavedCredential = false;
                _isCredentialStateLoading = false;
                RememberPassword = false;
                NotifyGameReadiness();
            }

            _ = LoadServersAsync(forceRefresh: false);
        }
    }

    public ProfileItemViewModel? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            PlatformItemViewModel previousPlatform = SelectedPlatform;
            if (!SetProperty(ref _selectedProfile, value))
            {
                return;
            }

            _serverIdBeforeFilter = null;
            ApplySelectedProfile();
            _ = PersistSelectedProfileAsync(value?.Model.Id);
            if (value is not null)
            {
                IsProfileEditorVisible = false;
            }

            OnPropertyChanged(nameof(SelectedProfileSummary));
            OnPropertyChanged(nameof(SelectedProfileLoginSummary));
            OnPropertyChanged(nameof(PrimaryActionLabel));
            OnPropertyChanged(nameof(CredentialStatusText));
            if (ReferenceEquals(previousPlatform, SelectedPlatform))
            {
                RefreshRecentServers();
            }

            DeleteProfileCommand.NotifyCanExecuteChanged();
            EditProfileCommand.NotifyCanExecuteChanged();
            NotifyGameReadiness();
            if (!_suppressProfileCatalogReload && ReferenceEquals(previousPlatform, SelectedPlatform))
            {
                _ = LoadServersAsync(forceRefresh: false);
            }
        }
    }

    public ServerRowViewModel? SelectedServer
    {
        get => _selectedServer;
        set
        {
            if (value is not null && !value.CanLaunch)
            {
                return;
            }

            if (SetProperty(ref _selectedServer, value))
            {
                OnPropertyChanged(nameof(SelectedServerDisplayName));
                NotifyGameReadiness();
            }
        }
    }

    public IReadOnlyList<ServerRowViewModel> VisibleServers
    {
        get => _visibleServers;
        private set
        {
            if (SetProperty(ref _visibleServers, value))
            {
                OnPropertyChanged(nameof(ServerCountLabel));
                OnPropertyChanged(nameof(ShowEmptyState));
            }
        }
    }

    public IReadOnlyList<ServerRowViewModel> RecentServers
    {
        get => _recentServers;
        private set
        {
            if (SetProperty(ref _recentServers, value))
            {
                SelectRecentServerCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            string normalized = value ?? string.Empty;
            bool wasFiltering = !string.IsNullOrWhiteSpace(_searchText);
            bool willFilter = !string.IsNullOrWhiteSpace(normalized);
            if (SetProperty(ref _searchText, normalized))
            {
                if (!wasFiltering && willFilter)
                {
                    _serverIdBeforeFilter = SelectedServer?.Id;
                }
                else if (wasFiltering && !willFilter)
                {
                    _pendingServerId = _serverIdBeforeFilter;
                    _serverIdBeforeFilter = null;
                }

                ApplyServerFilter();
                if (wasFiltering && !willFilter)
                {
                    RestoreServerSelection(catalog: null);
                }
            }
        }
    }

    public string ProfileLabel
    {
        get => _profileLabel;
        set => SetProperty(ref _profileLabel, value ?? string.Empty);
    }

    public string LoginHint
    {
        get => _loginHint;
        set
        {
            if (SetProperty(ref _loginHint, value ?? string.Empty))
            {
                if (SelectedProfile?.Model is { } profile && IsSelectedProfileIdentity(profile))
                {
                    BeginCredentialStateLoad(profile);
                }
                else
                {
                    HasSavedCredential = false;
                    _isCredentialStateLoading = false;
                    RememberPassword = false;
                }

                NotifyGameReadiness();
            }
        }
    }

    public string PendingPassword
    {
        get => _pendingPassword;
        set
        {
            if (SetProperty(ref _pendingPassword, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(PasswordPlaceholderText));
                NotifyGameReadiness();
            }
        }
    }

    public bool RememberPassword
    {
        get => _rememberPassword;
        set => SetProperty(ref _rememberPassword, value);
    }

    public bool HasSavedCredential
    {
        get => _hasSavedCredential;
        private set
        {
            if (!SetProperty(ref _hasSavedCredential, value))
            {
                return;
            }

            OnPropertyChanged(nameof(PasswordPlaceholderText));
            OnPropertyChanged(nameof(CredentialStatusText));
            NotifyGameReadiness();
        }
    }

    public bool IsWorkspaceVisible
    {
        get => _isWorkspaceVisible;
        private set
        {
            if (SetProperty(ref _isWorkspaceVisible, value))
            {
                OnPropertyChanged(nameof(IsLauncherVisible));
            }
        }
    }

    public bool IsLauncherVisible => !IsWorkspaceVisible;

    public bool IsProfileEditorVisible
    {
        get => _isProfileEditorVisible;
        private set
        {
            if (SetProperty(ref _isProfileEditorVisible, value))
            {
                OnPropertyChanged(nameof(IsProfileReadyVisible));
            }
        }
    }

    public bool IsProfileReadyVisible => !IsProfileEditorVisible;

    public string SelectedProfileSummary =>
        SelectedProfile?.DisplayName ?? _localization.Get("Profiles_NewProfile");

    public string SelectedProfileLoginSummary => SelectedProfile?.Model.UserName ?? LoginHint;

    public string CredentialStatusText => HasActiveSelectedSession
        ? _localization.Get("Credentials_ActiveSession")
        : HasSavedCredential
            ? _localization.Get("Credentials_Stored")
            : _localization.Get("Credentials_Required");

    public string PrimaryActionLabel => HasActiveSelectedSession
        ? _localization.Get("Action_BackToGame")
        : _localization.Get("Action_EnterAndPlay");

    public string PasswordPlaceholderText => HasSavedCredential && PendingPassword.Length == 0
        ? _localization.Get("Credentials_PasswordSavedPlaceholder")
        : _localization.Get("Credentials_PasswordEnterPlaceholder");

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (!SetProperty(ref _isLoading, value))
            {
                return;
            }

            RefreshServersCommand.NotifyCanExecuteChanged();
            SaveProfileCommand.NotifyCanExecuteChanged();
            DeleteProfileCommand.NotifyCanExecuteChanged();
            SelectRecentServerCommand.NotifyCanExecuteChanged();
            NotifyGameReadiness();
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    public bool IsLaunching
    {
        get => _isLaunching;
        private set
        {
            if (!SetProperty(ref _isLaunching, value))
            {
                return;
            }

            NewProfileCommand.NotifyCanExecuteChanged();
            AddAccountCommand.NotifyCanExecuteChanged();
            EditProfileCommand.NotifyCanExecuteChanged();
            CancelProfileEditCommand.NotifyCanExecuteChanged();
            RefreshServersCommand.NotifyCanExecuteChanged();
            SaveProfileCommand.NotifyCanExecuteChanged();
            DeleteProfileCommand.NotifyCanExecuteChanged();
            SelectRecentServerCommand.NotifyCanExecuteChanged();
            InstallUpdateCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(UpdateDetailText));
            NotifyGameReadiness();
        }
    }

    public Brush CatalogStatusBrush
    {
        get => _catalogStatusBrush;
        private set => SetProperty(ref _catalogStatusBrush, value);
    }

    public string ServerCountLabel => _localization.Format(
        VisibleServers.Count == 1 ? "Servers_CountSingular" : "Servers_CountPlural",
        VisibleServers.Count);

    public bool ShowEmptyState => !IsLoading && VisibleServers.Count == 0;

    public string RuntimeStatusShort => _runtimeProbe.IsUsable
        ? _localization.Get("Runtime_Detected")
        : _localization.Get("Runtime_Pending");

    public string RuntimeStatusDetail => _runtimeProbe.IsUsable
        ? _localization.Get("Runtime_DetectedDetail")
        : _localization.Get("Runtime_MissingDetail");

    public Brush RuntimeStatusBrush => _runtimeProbe.IsUsable ? WarningBrush : MutedBrush;

    public bool CanStartGame =>
        !_disposed &&
        !IsUpdateOperationActive &&
        (HasActiveSelectedSession ||
        (
        !IsLoading &&
        !IsLaunching &&
        !_isCredentialStateLoading &&
        _runtimeProbe.IsUsable &&
        !string.IsNullOrWhiteSpace(_runtimeProbe.RuntimeDirectory) &&
        SelectedServer?.CanLaunch == true &&
        !string.IsNullOrWhiteSpace(LoginHint) &&
        (PendingPassword.Length > 0 || HasSavedCredential)));

    private bool HasActiveSelectedSession =>
        SelectedProfile?.Model is { } selectedProfile &&
        SelectedServer?.Model is { } selectedServer &&
        Workspace.Sessions.Any(session =>
            session.ProfileId == selectedProfile.Id &&
            string.Equals(
                session.PlatformId,
                SelectedPlatform.Id,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                session.ServerId,
                selectedServer.Id,
                StringComparison.OrdinalIgnoreCase) &&
            session.IsRunning);

    public async Task InitializeAsync()
    {
        LauncherSettingsSnapshot settings = LauncherSettingsSnapshot.Default;
        try
        {
            settings = await _settingsService.LoadAsync().ConfigureAwait(true);
            ApplyLanguageSettings(settings.LanguageCode);
            Workspace.ApplySettings(settings);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            SetStatusMessage("Settings_LoadFailed");
        }

        BeginUpdateCheck();
        await EvaluateDonationPromptOnOpeningAsync(settings).ConfigureAwait(true);

        try
        {
            await LoadProfilesAsync(settings.LastSelectedProfileId).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Profile loading failed: {exception}");
            SetStatusMessage("Profiles_LoadFailed");
            CatalogStatusBrush = ErrorBrush;
        }
        await LoadServersAsync(forceRefresh: false).ConfigureAwait(true);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _catalogCancellation?.Cancel();
        _catalogCancellation?.Dispose();
        _launchCancellation?.Cancel();
        _launchCancellation?.Dispose();
        Workspace.SessionRemoved -= WorkspaceOnSessionRemoved;
        DisposeUpdater();
        DisposeLocalization();
        Workspace.Dispose();
    }

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
                SetCatalogStatus("Auth_PasswordRequired");
                CatalogStatusBrush = WarningBrush;
                SetStatusMessage("Auth_PasswordRequiredMessage");
                return;
            }

            if (outcome.State == SessionLaunchState.AuthenticationRejected)
            {
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
            if (outcome.WasProfilePersisted)
            {
                if (requestedProfile?.Model.Id == launchedProfile.Id)
                {
                    ReplaceProfile(requestedProfile, launchedProfile);
                }
                else
                {
                    await LoadProfilesAsync(launchedProfile.Id).ConfigureAwait(true);
                }

                await LoadServersAsync(forceRefresh: false).ConfigureAwait(true);
            }

            PendingPassword = string.Empty;
            Workspace.AddSession(
                launchedProfile,
                launchedPlatform,
                launchedServer,
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
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SetCatalogStatus("Game_Cancelled");
            CatalogStatusBrush = MutedBrush;
            SetStatusMessage("Game_CancelledMessage");
        }
        catch (Exception)
        {
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

    private static string BuildAuthenticationFailureKey(string? errorCode)
    {
        if (IsCredentialRejection(errorCode))
        {
            return "Auth_CredentialRejected";
        }

        return errorCode switch
        {
            "unsupported_platform" => "Auth_UnsupportedPlatform",
            "invalid_server" => "Auth_InvalidServer",
            "http_error" => "Auth_HttpError",
            "network_error" => "Auth_NetworkError",
            "request_timeout" => "Auth_Timeout",
            "response_too_large" => "Auth_ResponseTooLarge",
            "invalid_authentication_response" => "Auth_InvalidResponse",
            "invalid_launch_response" => "Auth_InvalidLaunch",
            "origin_not_allowed" => "Auth_OriginNotAllowed",
            "sevenwan_service_unavailable" => "Auth_SevenWanUnavailable",
            null or "" => "Auth_NotConfirmed",
            _ => "Auth_NotConfirmedCode",
        };
    }

    private static bool IsCredentialRejection(string? errorCode) =>
        string.Equals(errorCode, "invalid_credentials", StringComparison.Ordinal) ||
        string.Equals(errorCode, "authentication_rejected", StringComparison.Ordinal);

    private void NotifyGameReadiness()
    {
        OnPropertyChanged(nameof(CanStartGame));
        OnPropertyChanged(nameof(PrimaryActionLabel));
        OnPropertyChanged(nameof(CredentialStatusText));
        PlayCommand.NotifyCanExecuteChanged();
    }

    private void WorkspaceOnSessionRemoved(object? sender, GameSessionViewModel session)
    {
        ShowWorkspaceCommand.NotifyCanExecuteChanged();
        if (!Workspace.HasSessions)
        {
            IsWorkspaceVisible = false;
        }

        NotifyGameReadiness();
    }

    private static Brush CreateBrush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }
}

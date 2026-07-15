using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Windows.Media;
using LegendLauncher.App.Updates;

namespace LegendLauncher.App.ViewModels;

internal sealed partial class MainWindowViewModel
{
    private ILauncherUpdateService _updateService = null!;
    private Version _currentVersion = null!;
    private CancellationTokenSource? _updateCancellation;
    private LauncherUpdateRelease? _availableUpdate;
    private DownloadedLauncherInstaller? _downloadedInstaller;
    private LauncherUpdateState _updateState;
    private bool _isUpdateNotesOpen;
    private double _updateProgress;

    internal event EventHandler? UpdateInstallerStarted;

    public AsyncRelayCommand CheckForUpdatesCommand { get; private set; } = null!;

    public AsyncRelayCommand InstallUpdateCommand { get; private set; } = null!;

    public RelayCommand ToggleUpdateNotesCommand { get; private set; } = null!;

    public RelayCommand CloseUpdateNotesCommand { get; private set; } = null!;

    public bool IsUpdateCardVisible => _updateState != LauncherUpdateState.Idle;

    public bool IsUpdateReadyToInstall =>
        _updateState == LauncherUpdateState.ReadyToInstall;

    public bool IsUpdateChecking => _updateState == LauncherUpdateState.Checking;

    public bool IsUpdateDownloading => _updateState == LauncherUpdateState.Downloading;

    public bool IsUpdateCheckActionVisible => _updateState is
        LauncherUpdateState.Current or
        LauncherUpdateState.Failed;

    private bool IsUpdateOperationActive =>
        _updateState == LauncherUpdateState.Installing;

    public bool IsUpdateProgressVisible => _updateState == LauncherUpdateState.Downloading;

    public bool IsUpdateNotesOpen
    {
        get => _isUpdateNotesOpen;
        set
        {
            if (SetProperty(ref _isUpdateNotesOpen, value))
            {
                CloseUpdateNotesCommand?.NotifyCanExecuteChanged();
            }
        }
    }

    public double UpdateProgress
    {
        get => _updateProgress;
        private set
        {
            double normalized = Math.Clamp(value, 0, 100);
            if (SetProperty(ref _updateProgress, normalized))
            {
                OnPropertyChanged(nameof(UpdateDetailText));
            }
        }
    }

    public Brush UpdateStatusBrush => _updateState switch
    {
        LauncherUpdateState.Current => OnlineBrush,
        LauncherUpdateState.Downloading or
        LauncherUpdateState.ReadyToInstall or
        LauncherUpdateState.Installing => WarningBrush,
        LauncherUpdateState.Failed => ErrorBrush,
        _ => MutedBrush,
    };

    public string UpdateEyebrowText => _localization.Get("Update_Eyebrow");

    public string UpdateTitleText => _updateState switch
    {
        LauncherUpdateState.Checking => _localization.Get("Update_CheckingTitle"),
        LauncherUpdateState.Current => _localization.Get("Update_CurrentTitle"),
        LauncherUpdateState.Downloading => _localization.Get("Update_DownloadingTitle"),
        LauncherUpdateState.ReadyToInstall => _localization.Format(
            "Update_ReadyTitle",
            _availableUpdate?.Version.ToString(3) ?? string.Empty),
        LauncherUpdateState.Installing => _localization.Get("Update_InstallingTitle"),
        LauncherUpdateState.Failed => _localization.Get("Update_FailedTitle"),
        _ => string.Empty,
    };

    public string UpdateDetailText => _updateState switch
    {
        LauncherUpdateState.Checking => _localization.Get("Update_CheckingDetail"),
        LauncherUpdateState.Current => _localization.Format(
            "Update_CurrentDetail",
            _currentVersion.ToString(3)),
        LauncherUpdateState.Downloading => _localization.Format(
            "Update_DownloadingDetail",
            Math.Round(UpdateProgress)),
        LauncherUpdateState.ReadyToInstall when IsLaunching || Workspace.HasSessions =>
            _localization.Get("Update_CloseSessions"),
        LauncherUpdateState.ReadyToInstall => _localization.Get("Update_ReadyDetail"),
        LauncherUpdateState.Installing => _localization.Get("Update_InstallingDetail"),
        LauncherUpdateState.Failed => _localization.Get("Update_FailedDetail"),
        _ => string.Empty,
    };

    public string UpdateNotesText =>
        _availableUpdate?.GetNotes(_localization.LanguageCode) ?? string.Empty;

    public string UpdateNotesTitleText => _localization.Format(
        "Update_NotesTitle",
        _availableUpdate?.Version.ToString(3) ?? string.Empty);

    public string UpdateActionText => _localization.Get("Update_Action");

    public string UpdateCheckActionText => _localization.Get(
        _updateState == LauncherUpdateState.Current
            ? "Update_CheckAgain"
            : "Update_Retry");

    public string UpdateViewNotesText => _localization.Get("Update_ViewNotes");

    public string UpdateCloseNotesText => _localization.Get("Update_CloseNotes");

    public string UpdateSessionWarningText => _localization.Get("Update_SessionWarning");

    private void InitializeUpdater(
        ILauncherUpdateService? updateService,
        Version? currentVersion)
    {
        _updateService = updateService ?? DisabledLauncherUpdateService.Instance;
        _currentVersion = currentVersion ??
            typeof(MainWindowViewModel).Assembly.GetName().Version ??
            new Version(1, 0, 0);
        CheckForUpdatesCommand = new AsyncRelayCommand(
            CheckForUpdatesAsync,
            CanCheckForUpdates);
        InstallUpdateCommand = new AsyncRelayCommand(
            InstallUpdateAsync,
            CanInstallUpdate);
        ToggleUpdateNotesCommand = new RelayCommand(
            () => IsUpdateNotesOpen = !IsUpdateNotesOpen,
            () => _availableUpdate is not null);
        CloseUpdateNotesCommand = new RelayCommand(
            () => IsUpdateNotesOpen = false,
            () => IsUpdateNotesOpen);
        Workspace.PropertyChanged += WorkspaceOnUpdatePropertyChanged;
    }

    private void BeginUpdateCheck()
    {
        if (!_disposed &&
            !ReferenceEquals(_updateService, DisabledLauncherUpdateService.Instance))
        {
            _ = CheckForUpdatesAsync();
        }
    }

    internal async Task CheckForUpdatesAsync()
    {
        if (_disposed)
        {
            return;
        }

        CancellationTokenSource cancellation = ReplaceUpdateCancellation();
        _availableUpdate = null;
        _downloadedInstaller = null;
        IsUpdateNotesOpen = false;
        UpdateProgress = 0;
        SetUpdateState(LauncherUpdateState.Checking);
        try
        {
            LauncherUpdateRelease? release = await _updateService
                .CheckForUpdateAsync(_currentVersion, cancellation.Token)
                .ConfigureAwait(true);
            if (cancellation.IsCancellationRequested || _disposed)
            {
                return;
            }

            if (release is null)
            {
                SetUpdateState(LauncherUpdateState.Current);
                return;
            }

            _availableUpdate = release;
            SetUpdateState(LauncherUpdateState.Downloading);
            var progress = new Progress<double>(value => UpdateProgress = value * 100);
            _downloadedInstaller = await _updateService
                .DownloadInstallerAsync(release, progress, cancellation.Token)
                .ConfigureAwait(true);
            if (cancellation.IsCancellationRequested || _disposed)
            {
                return;
            }

            SetUpdateState(LauncherUpdateState.ReadyToInstall);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (IsExpectedUpdateException(exception))
        {
            System.Diagnostics.Debug.WriteLine($"Update preparation failed: {exception.Message}");
            SetUpdateState(LauncherUpdateState.Failed);
        }
        finally
        {
            ReleaseUpdateCancellation(cancellation);
        }
    }

    internal async Task InstallUpdateAsync()
    {
        if (_downloadedInstaller is null ||
            _updateState != LauncherUpdateState.ReadyToInstall ||
            IsLaunching ||
            Workspace.HasSessions)
        {
            OnPropertyChanged(nameof(UpdateDetailText));
            return;
        }

        CancellationTokenSource cancellation = ReplaceUpdateCancellation();
        IsUpdateNotesOpen = false;
        SetUpdateState(LauncherUpdateState.Installing);
        try
        {
            await _updateService
                .LaunchInstallerAsync(_downloadedInstaller, cancellation.Token)
                .ConfigureAwait(true);
            UpdateInstallerStarted?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (IsExpectedUpdateException(exception))
        {
            System.Diagnostics.Debug.WriteLine($"Update installation failed: {exception.Message}");
            SetUpdateState(LauncherUpdateState.Failed);
        }
        finally
        {
            ReleaseUpdateCancellation(cancellation);
        }
    }

    private bool CanCheckForUpdates() =>
        !_disposed && _updateState is not (
            LauncherUpdateState.Checking or
            LauncherUpdateState.Downloading or
            LauncherUpdateState.Installing);

    private bool CanInstallUpdate() =>
        !_disposed &&
        _availableUpdate is not null &&
        _downloadedInstaller is not null &&
        _updateState == LauncherUpdateState.ReadyToInstall &&
        !IsLaunching &&
        !Workspace.HasSessions;

    private void SetUpdateState(LauncherUpdateState state)
    {
        _updateState = state;
        if (state != LauncherUpdateState.ReadyToInstall)
        {
            IsUpdateNotesOpen = false;
        }

        RefreshUpdateProperties();
    }

    private void RefreshUpdateProperties()
    {
        OnPropertyChanged(nameof(IsUpdateCardVisible));
        OnPropertyChanged(nameof(IsUpdateReadyToInstall));
        OnPropertyChanged(nameof(IsUpdateChecking));
        OnPropertyChanged(nameof(IsUpdateDownloading));
        OnPropertyChanged(nameof(IsUpdateProgressVisible));
        OnPropertyChanged(nameof(IsUpdateCheckActionVisible));
        OnPropertyChanged(nameof(UpdateStatusBrush));
        OnPropertyChanged(nameof(UpdateEyebrowText));
        OnPropertyChanged(nameof(UpdateTitleText));
        OnPropertyChanged(nameof(UpdateDetailText));
        OnPropertyChanged(nameof(UpdateNotesText));
        OnPropertyChanged(nameof(UpdateNotesTitleText));
        OnPropertyChanged(nameof(UpdateActionText));
        OnPropertyChanged(nameof(UpdateCheckActionText));
        OnPropertyChanged(nameof(UpdateViewNotesText));
        OnPropertyChanged(nameof(UpdateCloseNotesText));
        OnPropertyChanged(nameof(UpdateSessionWarningText));
        CheckForUpdatesCommand.NotifyCanExecuteChanged();
        InstallUpdateCommand.NotifyCanExecuteChanged();
        ToggleUpdateNotesCommand.NotifyCanExecuteChanged();
        CloseUpdateNotesCommand.NotifyCanExecuteChanged();
        PlayCommand.NotifyCanExecuteChanged();
    }

    private CancellationTokenSource ReplaceUpdateCancellation()
    {
        _updateCancellation?.Cancel();
        _updateCancellation?.Dispose();
        _updateCancellation = new CancellationTokenSource();
        return _updateCancellation;
    }

    private void ReleaseUpdateCancellation(CancellationTokenSource cancellation)
    {
        if (ReferenceEquals(_updateCancellation, cancellation))
        {
            _updateCancellation = null;
        }

        cancellation.Dispose();
    }

    private void WorkspaceOnUpdatePropertyChanged(
        object? sender,
        PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(GameWorkspaceViewModel.HasSessions))
        {
            OnPropertyChanged(nameof(UpdateDetailText));
            InstallUpdateCommand.NotifyCanExecuteChanged();
        }
    }

    private void DisposeUpdater()
    {
        Workspace.PropertyChanged -= WorkspaceOnUpdatePropertyChanged;
        _updateCancellation?.Cancel();
        _updateCancellation?.Dispose();
        _updateCancellation = null;
    }

    private static bool IsExpectedUpdateException(Exception exception) =>
        exception is HttpRequestException or
            IOException or
            InvalidDataException or
            InvalidOperationException or
            UnauthorizedAccessException or
            System.Text.Json.JsonException;

    private enum LauncherUpdateState
    {
        Idle,
        Checking,
        Current,
        Downloading,
        ReadyToInstall,
        Installing,
        Failed,
    }

    private sealed class DisabledLauncherUpdateService : ILauncherUpdateService
    {
        internal static DisabledLauncherUpdateService Instance { get; } = new();

        public Task<LauncherUpdateRelease?> CheckForUpdateAsync(
            Version currentVersion,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<LauncherUpdateRelease?>(null);

        public Task<DownloadedLauncherInstaller> DownloadInstallerAsync(
            LauncherUpdateRelease release,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task LaunchInstallerAsync(
            DownloadedLauncherInstaller installer,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}

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
    private LauncherUpdateState _updateState;
    private bool _isUpdateNotesOpen;
    private double _updateProgress;

    internal event EventHandler? UpdateInstallerStarted;

    public AsyncRelayCommand CheckForUpdatesCommand { get; private set; } = null!;

    public AsyncRelayCommand InstallUpdateCommand { get; private set; } = null!;

    public RelayCommand ToggleUpdateNotesCommand { get; private set; } = null!;

    public RelayCommand CloseUpdateNotesCommand { get; private set; } = null!;

    public bool IsUpdateCardVisible => _updateState != LauncherUpdateState.Idle;

    public bool IsUpdateAvailable => _updateState == LauncherUpdateState.Available;

    public bool IsUpdateChecking => _updateState == LauncherUpdateState.Checking;

    public bool IsUpdateDownloading => _updateState == LauncherUpdateState.Downloading;

    private bool IsUpdateOperationActive => _updateState is
        LauncherUpdateState.Downloading or
        LauncherUpdateState.Installing;

    public bool IsUpdateProgressVisible => _updateState == LauncherUpdateState.Downloading;

    public bool IsUpdateRetryVisible => _updateState == LauncherUpdateState.Failed;

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
        LauncherUpdateState.Available or
        LauncherUpdateState.Downloading or
        LauncherUpdateState.Installing => WarningBrush,
        LauncherUpdateState.Failed => ErrorBrush,
        _ => MutedBrush,
    };

    public string UpdateEyebrowText => _localization.Get("Update_Eyebrow");

    public string UpdateTitleText => _updateState switch
    {
        LauncherUpdateState.Checking => _localization.Get("Update_CheckingTitle"),
        LauncherUpdateState.Current => _localization.Get("Update_CurrentTitle"),
        LauncherUpdateState.Available => _localization.Format(
            "Update_AvailableTitle",
            _availableUpdate?.Version.ToString(3) ?? string.Empty),
        LauncherUpdateState.Downloading => _localization.Get("Update_DownloadingTitle"),
        LauncherUpdateState.Installing => _localization.Get("Update_InstallingTitle"),
        LauncherUpdateState.Failed => _localization.Get("Update_FailedTitle"),
        _ => string.Empty,
    };

    public string UpdateDetailText => _updateState switch
    {
        LauncherUpdateState.Checking => _localization.Get("Update_CheckingDetail"),
        LauncherUpdateState.Current => _localization.Get("Update_CurrentDetail"),
        LauncherUpdateState.Available when Workspace.HasSessions =>
            _localization.Get("Update_CloseSessions"),
        LauncherUpdateState.Available => _localization.Get("Update_AvailableDetail"),
        LauncherUpdateState.Downloading => _localization.Format(
            "Update_DownloadingDetail",
            Math.Round(UpdateProgress)),
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

    public string UpdateRetryText => _localization.Get("Update_Retry");

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
        IsUpdateNotesOpen = false;
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

            _availableUpdate = release;
            SetUpdateState(release is null
                ? LauncherUpdateState.Current
                : LauncherUpdateState.Available);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (IsExpectedUpdateException(exception))
        {
            System.Diagnostics.Debug.WriteLine($"Update check failed: {exception.Message}");
            SetUpdateState(LauncherUpdateState.Failed);
        }
        finally
        {
            ReleaseUpdateCancellation(cancellation);
        }
    }

    internal async Task InstallUpdateAsync()
    {
        if (_availableUpdate is null || Workspace.HasSessions)
        {
            OnPropertyChanged(nameof(UpdateDetailText));
            return;
        }

        CancellationTokenSource cancellation = ReplaceUpdateCancellation();
        IsUpdateNotesOpen = false;
        UpdateProgress = 0;
        SetUpdateState(LauncherUpdateState.Downloading);
        try
        {
            var progress = new Progress<double>(value => UpdateProgress = value * 100);
            DownloadedLauncherInstaller installer = await _updateService
                .DownloadInstallerAsync(_availableUpdate, progress, cancellation.Token)
                .ConfigureAwait(true);
            if (cancellation.IsCancellationRequested || _disposed)
            {
                return;
            }

            if (Workspace.HasSessions)
            {
                SetUpdateState(LauncherUpdateState.Available);
                return;
            }

            SetUpdateState(LauncherUpdateState.Installing);
            await _updateService
                .LaunchInstallerAsync(installer, cancellation.Token)
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
        _updateState == LauncherUpdateState.Available &&
        !Workspace.HasSessions;

    private void SetUpdateState(LauncherUpdateState state)
    {
        _updateState = state;
        if (state != LauncherUpdateState.Available)
        {
            IsUpdateNotesOpen = false;
        }

        RefreshUpdateProperties();
    }

    private void RefreshUpdateProperties()
    {
        OnPropertyChanged(nameof(IsUpdateCardVisible));
        OnPropertyChanged(nameof(IsUpdateAvailable));
        OnPropertyChanged(nameof(IsUpdateChecking));
        OnPropertyChanged(nameof(IsUpdateDownloading));
        OnPropertyChanged(nameof(IsUpdateProgressVisible));
        OnPropertyChanged(nameof(IsUpdateRetryVisible));
        OnPropertyChanged(nameof(UpdateStatusBrush));
        OnPropertyChanged(nameof(UpdateEyebrowText));
        OnPropertyChanged(nameof(UpdateTitleText));
        OnPropertyChanged(nameof(UpdateDetailText));
        OnPropertyChanged(nameof(UpdateNotesText));
        OnPropertyChanged(nameof(UpdateNotesTitleText));
        OnPropertyChanged(nameof(UpdateActionText));
        OnPropertyChanged(nameof(UpdateRetryText));
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
        Available,
        Downloading,
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

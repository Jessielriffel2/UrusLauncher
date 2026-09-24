using System.Collections.ObjectModel;
using System.IO;
using System.Diagnostics;
using System.Windows;
using LegendLauncher.App.GameHosting;
using LegendLauncher.App.Localization;
using LegendLauncher.App.MacroAssistant;
using LegendLauncher.App.Services;
using LegendLauncher.Core.Models;

namespace LegendLauncher.App.ViewModels;

internal sealed class GameWorkspaceViewModel : ObservableObject, IDisposable
{
    private readonly GameAudioService _audioService;
    private readonly LauncherSettingsService _settingsService;
    private readonly LocalizationService _localization;
    private readonly Func<nint, int, GameWindowAttachment?> _attachmentFactory;
    private IReadOnlyList<GameSessionViewModel> _visibleSessions = [];
    private IReadOnlyList<WorkspaceAvatarItem> _sidebarAvatars = [];
    private IReadOnlyList<GameSessionViewModel> _splitPair = [];
    private GameSessionViewModel? _selectedSession;
    private GameLayoutMode _layoutMode = GameLayoutMode.GridFour;
    private bool _isMuted = true;
    private bool _disposed;

    public GameWorkspaceViewModel(
        GameAudioService audioService,
        LauncherSettingsService settingsService,
        Func<nint, int, GameWindowAttachment?>? attachmentFactory = null,
        LocalizationService? localization = null)
    {
        _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _localization = localization ?? LocalizationService.Current;
        _attachmentFactory = attachmentFactory ??
            ((windowHandle, processId) => new GameWindowAttachment(windowHandle, processId));
        Sessions = [];
        MacroAssistant = new MacroAssistantViewModel(_localization);
        _localization.LanguageChanged += LocalizationOnLanguageChanged;

        SelectSessionCommand = new RelayCommand<GameSessionViewModel>(
            SelectSession,
            session => session is not null && Sessions.Contains(session));
        ActivateSidebarAvatarCommand = new RelayCommand<WorkspaceAvatarItem>(
            ActivateSidebarAvatar,
            item => item is not null && Sessions.Contains(item.Front));
        CloseSessionCommand = new RelayCommand<GameSessionViewModel>(
            CloseSession,
            session => session is not null && Sessions.Contains(session));
        DetachSessionCommand = new RelayCommand<GameSessionViewModel>(
            DetachSession,
            session => session is not null && Sessions.Contains(session) && !session.IsDetached);
        RelogSessionCommand = new RelayCommand<GameSessionViewModel>(
            RelogSession,
            session => session is not null && Sessions.Contains(session) && session.IsRunning);
        ToggleMuteCommand = new RelayCommand(ToggleMute);
        SingleLayoutCommand = new RelayCommand(() => LayoutMode = GameLayoutMode.Single);
        SplitTwoLayoutCommand = new RelayCommand(() => LayoutMode = GameLayoutMode.SplitTwo);
        GridFourLayoutCommand = new RelayCommand(() => LayoutMode = GameLayoutMode.GridFour);
    }

    public event EventHandler<GameSessionViewModel>? DetachRequested;

    public event EventHandler<GameSessionViewModel>? RelogRequested;

    public event EventHandler<GameSessionViewModel>? SessionRemoved;

    public ObservableCollection<GameSessionViewModel> Sessions { get; }

    public MacroAssistantViewModel MacroAssistant { get; }

    public RelayCommand<GameSessionViewModel> SelectSessionCommand { get; }

    public RelayCommand<WorkspaceAvatarItem> ActivateSidebarAvatarCommand { get; }

    public RelayCommand<GameSessionViewModel> CloseSessionCommand { get; }

    public RelayCommand<GameSessionViewModel> DetachSessionCommand { get; }

    public RelayCommand<GameSessionViewModel> RelogSessionCommand { get; }

    public RelayCommand ToggleMuteCommand { get; }

    public RelayCommand SingleLayoutCommand { get; }

    public RelayCommand SplitTwoLayoutCommand { get; }

    public RelayCommand GridFourLayoutCommand { get; }

    public IReadOnlyList<GameSessionViewModel> VisibleSessions
    {
        get => _visibleSessions;
        private set
        {
            if (!SetProperty(ref _visibleSessions, value))
            {
                return;
            }

            OnPropertyChanged(nameof(LayoutRows));
            OnPropertyChanged(nameof(LayoutColumns));
        }
    }

    public IReadOnlyList<WorkspaceAvatarItem> SidebarAvatars
    {
        get => _sidebarAvatars;
        private set => SetProperty(ref _sidebarAvatars, value);
    }

    public GameSessionViewModel? SelectedSession
    {
        get => _selectedSession;
        set
        {
            if (value is not null && !Sessions.Contains(value))
            {
                return;
            }

            if (SetProperty(ref _selectedSession, value))
            {
                foreach (GameSessionViewModel session in Sessions)
                {
                    session.IsSelected = ReferenceEquals(session, value);
                }

                RefreshVisibleSessions();
                OnPropertyChanged(nameof(CanDetachSelected));
            }
        }
    }

    public GameLayoutMode LayoutMode
    {
        get => _layoutMode;
        set
        {
            if (!Enum.IsDefined(value) || !SetProperty(ref _layoutMode, value))
            {
                return;
            }

            OnPropertyChanged(nameof(LayoutRows));
            OnPropertyChanged(nameof(LayoutColumns));
            OnPropertyChanged(nameof(IsSingleLayout));
            OnPropertyChanged(nameof(IsSplitTwoLayout));
            OnPropertyChanged(nameof(IsGridFourLayout));
            RefreshVisibleSessions();
            _ = PersistGamePreferencesAsync();
        }
    }

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (!SetProperty(ref _isMuted, value))
            {
                return;
            }

            _audioService.SetMuted(value);
            OnPropertyChanged(nameof(MuteLabel));
            OnPropertyChanged(nameof(MuteGlyph));
            OnPropertyChanged(nameof(FooterStatus));
            _ = PersistGamePreferencesAsync();
        }
    }

    public int LayoutRows =>
        LayoutMode == GameLayoutMode.GridFour && VisibleSessions.Count > 2 ? 2 : 1;

    public int LayoutColumns =>
        LayoutMode == GameLayoutMode.Single || VisibleSessions.Count <= 1 ? 1 : 2;

    public bool IsSingleLayout => LayoutMode == GameLayoutMode.Single;

    public bool IsSplitTwoLayout => LayoutMode == GameLayoutMode.SplitTwo;

    public bool IsGridFourLayout => LayoutMode == GameLayoutMode.GridFour;

    public bool HasSessions => Sessions.Count > 0;

    public bool CanDetachSelected => SelectedSession is { IsDetached: false };

    public string MuteLabel => IsMuted
        ? _localization.Get("Workspace_MutedLabel")
        : _localization.Get("Workspace_ActiveSoundLabel");

    public string MuteGlyph => IsMuted ? "\uE74F" : "\uE995";

    public string FooterStatus => _localization.Format(
        "Workspace_FooterFormat",
        Sessions.Count,
        _localization.Get(Sessions.Count == 1
            ? "Workspace_ActiveSessionSingular"
            : "Workspace_ActiveSessionPlural"),
        _localization.Get(IsMuted
            ? "Workspace_GlobalSoundMuted"
            : "Workspace_GlobalSoundActive"),
        _localization.Get("Workspace_IsolatedProfiles"));

    public void ApplySettings(LauncherSettingsSnapshot settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _layoutMode = settings.LayoutMode;
        _isMuted = settings.IsGameMuted;
        _audioService.SetMuted(_isMuted);
        OnPropertyChanged(nameof(LayoutMode));
        OnPropertyChanged(nameof(LayoutRows));
        OnPropertyChanged(nameof(LayoutColumns));
        OnPropertyChanged(nameof(IsSingleLayout));
        OnPropertyChanged(nameof(IsSplitTwoLayout));
        OnPropertyChanged(nameof(IsGridFourLayout));
        OnPropertyChanged(nameof(IsMuted));
        OnPropertyChanged(nameof(MuteLabel));
        OnPropertyChanged(nameof(MuteGlyph));
        OnPropertyChanged(nameof(FooterStatus));
    }

    public GameSessionViewModel AddSession(
        AccountProfile profile,
        PlatformDefinition platform,
        GameServer server,
        GameSession session)
    {
        ThrowIfDisposed();
        GameWindowAttachment? attachment;
        try
        {
            attachment = _attachmentFactory(session.NativeWindowHandle, session.ProcessId);
        }
        catch
        {
            TryTerminateProcess(session.ProcessId);
            throw;
        }

        GameSessionViewModel item;
        try
        {
            item = new GameSessionViewModel(
                profile,
                platform,
                server,
                session,
                attachment);
        }
        catch
        {
            TryTerminateProcess(session.ProcessId);
            throw;
        }
        item.Exited += SessionOnExited;
        Sessions.Add(item);
        if (!item.IsRunning)
        {
            RemoveSession(item);
            throw new InvalidOperationException("The isolated GameHost exited before it could be embedded.");
        }

        _audioService.RegisterProcess(item.ProcessId);
        SelectedSession = item;
        NotifySessionCollectionChanged();
        return item;
    }

    public bool TryActivateSession(Guid profileId, string platformId, string serverId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platformId);
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);

        GameSessionViewModel? existing = Sessions.FirstOrDefault(session =>
            session.ProfileId == profileId &&
            string.Equals(session.PlatformId, platformId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(session.ServerId, serverId, StringComparison.OrdinalIgnoreCase) &&
            session.IsRunning);
        if (existing is null)
        {
            return false;
        }

        SelectedSession = existing;
        return true;
    }

    public void Reattach(GameSessionViewModel session)
    {
        if (!Sessions.Contains(session))
        {
            return;
        }

        session.IsDetached = false;
        SelectedSession = session;
        RefreshVisibleSessions();
        DetachSessionCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanDetachSelected));
    }

    public void CloseSelectedSession()
    {
        if (SelectedSession is not null)
        {
            CloseSession(SelectedSession);
        }
    }

    public void RequestClose(GameSessionViewModel session) => CloseSession(session);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _localization.LanguageChanged -= LocalizationOnLanguageChanged;
        MacroAssistant.Dispose();
        foreach (GameSessionViewModel session in Sessions.ToArray())
        {
            session.Exited -= SessionOnExited;
            session.Terminate();
            _audioService.UnregisterProcess(session.ProcessId);
            session.Dispose();
        }

        Sessions.Clear();
        _audioService.Dispose();
    }

    private void SelectSession(GameSessionViewModel? session)
    {
        if (session is not null)
        {
            SelectedSession = session;
        }
    }

    private void ActivateSidebarAvatar(WorkspaceAvatarItem? item)
    {
        if (item is null || !Sessions.Contains(item.Front))
        {
            return;
        }

        if (item.Rear is { } rear && Sessions.Contains(rear))
        {
            _splitPair = [item.Front, rear];
            if (LayoutMode != GameLayoutMode.SplitTwo)
            {
                LayoutMode = GameLayoutMode.SplitTwo;
                return;
            }

            if (ReferenceEquals(SelectedSession, item.Front) ||
                ReferenceEquals(SelectedSession, rear))
            {
                RefreshVisibleSessions();
                return;
            }

            SelectedSession = item.Front;
            return;
        }

        SelectSession(item.Front);
    }

    private void CloseSession(GameSessionViewModel? session)
    {
        if (session is null || !Sessions.Contains(session))
        {
            return;
        }

        session.Terminate();
        RemoveSession(session);
    }

    private void DetachSession(GameSessionViewModel? session)
    {
        if (session is null || !Sessions.Contains(session) || session.IsDetached)
        {
            return;
        }

        session.IsDetached = true;
        RefreshVisibleSessions();
        DetachSessionCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanDetachSelected));
        DetachRequested?.Invoke(this, session);
    }

    private void RelogSession(GameSessionViewModel? session)
    {
        if (session is null || !Sessions.Contains(session) || !session.IsRunning)
        {
            return;
        }

        RelogRequested?.Invoke(this, session);
    }

    private void ToggleMute() => IsMuted = !IsMuted;

    private void LocalizationOnLanguageChanged(object? sender, EventArgs eventArgs)
    {
        OnPropertyChanged(nameof(MuteLabel));
        OnPropertyChanged(nameof(FooterStatus));
        RefreshSidebarAvatars();
    }

    private void SessionOnExited(object? sender, EventArgs eventArgs)
    {
        if (sender is not GameSessionViewModel session)
        {
            return;
        }

        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            _ = dispatcher.BeginInvoke(() => RemoveSession(session));
            return;
        }

        RemoveSession(session);
    }

    private void RemoveSession(GameSessionViewModel session)
    {
        if (!Sessions.Remove(session))
        {
            return;
        }

        session.Exited -= SessionOnExited;
        _audioService.UnregisterProcess(session.ProcessId);
        session.Dispose();
        SessionRemoved?.Invoke(this, session);
        SelectedSession = Sessions.FirstOrDefault(candidate => !candidate.IsDetached) ??
            Sessions.FirstOrDefault();
        NotifySessionCollectionChanged();
    }

    private void RefreshVisibleSessions()
    {
        int capacity = (int)LayoutMode;
        List<GameSessionViewModel> running = Sessions
            .Where(static session => !session.IsDetached && session.IsRunning)
            .ToList();
        List<GameSessionViewModel> candidates;
        if (LayoutMode == GameLayoutMode.SplitTwo &&
            _splitPair.Count == 2 &&
            _splitPair.All(running.Contains))
        {
            candidates = [.. _splitPair];
            if (SelectedSession is { IsDetached: false, IsRunning: true } selected &&
                !candidates.Contains(selected))
            {
                candidates[^1] = selected;
            }
        }
        else
        {
            candidates = running.Take(capacity).ToList();
            if (SelectedSession is { IsDetached: false, IsRunning: true } selected &&
                !candidates.Contains(selected))
            {
                if (candidates.Count == capacity && candidates.Count > 0)
                {
                    candidates[^1] = selected;
                }
                else
                {
                    candidates.Add(selected);
                }
            }
        }

        _splitPair = LayoutMode == GameLayoutMode.SplitTwo && candidates.Count == 2
            ? candidates
            : [];
        VisibleSessions = candidates;
        RefreshSidebarAvatars();
    }

    private void RefreshSidebarAvatars()
    {
        List<WorkspaceAvatarItem> items = [];
        HashSet<GameSessionViewModel> grouped = [];
        if (LayoutMode == GameLayoutMode.SplitTwo && VisibleSessions.Count == 2)
        {
            items.Add(CreatePairAvatar(VisibleSessions[0], VisibleSessions[1]));
            grouped.Add(VisibleSessions[0]);
            grouped.Add(VisibleSessions[1]);
        }

        foreach (GameSessionViewModel session in Sessions)
        {
            if (!grouped.Contains(session))
            {
                items.Add(CreateSingleAvatar(session));
            }
        }

        SidebarAvatars = items;
    }

    private WorkspaceAvatarItem CreateSingleAvatar(GameSessionViewModel session) =>
        new(
            session,
            null,
            session.SurfaceTitle,
            FormatAvatarAutomation(session.SurfaceTitle, session.IsSelected));

    private WorkspaceAvatarItem CreatePairAvatar(
        GameSessionViewModel left,
        GameSessionViewModel right)
    {
        string pairAutomation = _localization.Format(
            "Workspace_SplitPairAutomation",
            left.ProfileName,
            right.ProfileName);
        return new(
            left,
            right,
            _localization.Format("Workspace_SplitPairTooltip", left.ProfileName, right.ProfileName),
            FormatAvatarAutomation(
                pairAutomation,
                left.IsSelected || right.IsSelected));
    }

    private string FormatAvatarAutomation(string name, bool selected) =>
        selected
            ? _localization.Format("Workspace_SelectedAvatarAutomation", name)
            : name;

    private void NotifySessionCollectionChanged()
    {
        RefreshVisibleSessions();
        OnPropertyChanged(nameof(HasSessions));
        OnPropertyChanged(nameof(FooterStatus));
        SelectSessionCommand.NotifyCanExecuteChanged();
        ActivateSidebarAvatarCommand.NotifyCanExecuteChanged();
        CloseSessionCommand.NotifyCanExecuteChanged();
        DetachSessionCommand.NotifyCanExecuteChanged();
        RelogSessionCommand.NotifyCanExecuteChanged();
    }

    private async Task PersistGamePreferencesAsync()
    {
        try
        {
            await _settingsService
                .SaveGamePreferencesAsync(IsMuted, LayoutMode)
                .ConfigureAwait(false);
        }
        catch (IOException)
        {
            // A preference failure must not interrupt a running game.
        }
        catch (UnauthorizedAccessException)
        {
            // Keep the in-memory preference for this run.
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static void TryTerminateProcess(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or
            System.ComponentModel.Win32Exception)
        {
            // Preserve the attachment failure.
        }
    }
}

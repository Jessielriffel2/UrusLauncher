using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using LegendLauncher.App.GameHosting;
using LegendLauncher.App.Services;
using LegendLauncher.App.ViewModels;
using LegendLauncher.Infrastructure.Logging;

namespace LegendLauncher.App.MacroAssistant;

internal sealed class MacroSessionController : ObservableObject, IMacroSession
{
    private static readonly TimeSpan GemsScanInterval = TimeSpan.FromMilliseconds(220);
    private static readonly TimeSpan CosmoScanInterval = TimeSpan.FromMilliseconds(320);
    private static readonly TimeSpan BoardChangeGrace = TimeSpan.FromMilliseconds(1000);
    private static readonly TimeSpan SetupRefreshInterval = TimeSpan.FromMilliseconds(120);

    private readonly GameSessionViewModel _session;
    private readonly Window _uiOwner;
    private readonly Dispatcher _dispatcher;
    private readonly GameSurfaceCapture _capture = new();
    private readonly DirectGameInput _input = new();
    private readonly MacroOverlayWindow _overlay;
    private readonly MacroSetupWindow _setup;
    private readonly DispatcherTimer _setupTimer;
    private readonly MacroBridgeOptions _bridgeOptions;
    private readonly ProfilePreferencesStore? _profilePreferences;
    private readonly Guid _profileId;
    private readonly DispatcherTimer _saveTimer;
    private CancellationTokenSource? _cancellation;
    private Task? _runTask;
    private ClickMacroSettings _clickSettings = new(0, TimeSpan.FromSeconds(0.10));
    private SurfaceGeometry _surface;
    private SurfaceRegion _frame;
    private MacroPoint _target;
    private MacroProfilePreferences _preferences = MacroProfilePreferences.Default;
    private bool _useLargeFrame;
    private double _speed = 1.0;
    private MacroMode _mode = MacroMode.Gems;
    private MacroSessionState _state = MacroSessionState.Stopped;
    private string _statusText = "Parado";
    private bool _overlayVisible = true;
    private bool _disposed;
    private bool _openingSetup;

    public MacroSessionController(
        GameSessionViewModel session,
        Window uiOwner,
        ProfilePreferencesStore? profilePreferences = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        ArgumentNullException.ThrowIfNull(uiOwner);
        _uiOwner = uiOwner;
        _profileId = session.ProfileId;
        _profilePreferences = profilePreferences;
        _dispatcher = uiOwner.Dispatcher;
        _overlay = new MacroOverlayWindow(session.Id, uiOwner);
        _setup = new MacroSetupWindow(session.TabTitle, uiOwner);
        _setup.PlayRequested += SetupOnPlayRequested;
        _setup.StopRequested += SetupOnStopRequested;
        _setup.RegionChanged += SetupOnRegionChanged;
        _setup.SettingsChanged += SetupOnSettingsChanged;
        _setupTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = SetupRefreshInterval,
        };
        _setupTimer.Tick += SetupTimerOnTick;
        _saveTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(350),
        };
        _saveTimer.Tick += SaveTimerOnTick;
        _bridgeOptions = GemMacroBridgeLocator.Resolve();
        _session.PropertyChanged += SessionOnPropertyChanged;
    }

    public GameSessionViewModel Session => _session;

    public string SessionTitle => _session.TabTitle;

    public MacroMode Mode => _mode;

    public SurfaceRegion Frame => _frame;

    public bool IsActive => _state is MacroSessionState.Configuring or MacroSessionState.Starting or MacroSessionState.Running;

    public MacroSessionState State
    {
        get => _state;
        private set
        {
            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(IsActive));
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool OverlayVisible
    {
        get => _overlayVisible;
        set
        {
            if (SetProperty(ref _overlayVisible, value) && !value)
            {
                _ = _dispatcher.InvokeAsync(_overlay.HideOverlay);
            }
        }
    }

    public void SetMode(MacroMode mode)
    {
        if (_mode == mode)
        {
            return;
        }

        if (_runTask is not null)
        {
            Stop();
        }

        CaptureSetupState();
        _saveTimer.Stop();
        _ = PersistPreferencesAsync(_mode, _preferences);
        _mode = mode;
        _setup.SetMode(mode);
        if (_state == MacroSessionState.Configuring && _surface.HasArea)
        {
            _ = ApplyModePreferencesAsync(mode);
        }
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_runTask is not null)
        {
            SetError("O macro já está em execução.");
            return;
        }

        if (_openingSetup)
        {
            return;
        }

        if (!_session.IsRunning)
        {
            SetError("A sessão do jogo não está mais em execução.");
            return;
        }

        if (!IsOwnerAvailable())
        {
            SetError("O launcher está minimizado ou oculto; o macro não pode iniciar.");
            return;
        }

        _openingSetup = true;
        _ = OpenSetupAsync();
    }

    public void Stop()
    {
        CancelSafely(_cancellation);
        _setupTimer.Stop();
        _saveTimer.Stop();
        CaptureSetupState();
        _ = PersistPreferencesAsync();
        _ = _dispatcher.InvokeAsync(() =>
        {
            _setup.HideSetup();
            _overlay.HideOverlay();
            if (!_disposed)
            {
                SetState(MacroSessionState.Stopped);
                SetStatus("Parado");
            }
        });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _setupTimer.Stop();
        _setupTimer.Tick -= SetupTimerOnTick;
        _saveTimer.Stop();
        _saveTimer.Tick -= SaveTimerOnTick;
        CaptureSetupState();
        _ = PersistPreferencesAsync();
        _setup.PlayRequested -= SetupOnPlayRequested;
        _setup.StopRequested -= SetupOnStopRequested;
        _setup.RegionChanged -= SetupOnRegionChanged;
        _setup.SettingsChanged -= SetupOnSettingsChanged;
        _session.PropertyChanged -= SessionOnPropertyChanged;
        CancelSafely(_cancellation);
        _ = _dispatcher.InvokeAsync(() =>
        {
            _setup.Close();
            _overlay.Close();
        });
    }

    private async Task OpenSetupAsync()
    {
        if (_state == MacroSessionState.Configuring || _disposed)
        {
            return;
        }

        try
        {
            GameWindowAttachment? attachment = _session.Attachment;
            if (attachment is null)
            {
                SetError("A sessão não possui uma superfície anexada.");
                return;
            }

            if (!IsExecutionSurfaceAvailable(attachment))
            {
                SetError("A superfície da sessão não está visível.");
                return;
            }

            SurfaceGeometry surface = _capture.GetGeometry(attachment.GameWindow);
            if (!surface.HasArea)
            {
                SetError("A superfície da sessão ainda não está visível.");
                return;
            }

            MacroProfilePreferences preferences = await LoadPreferencesAsync(_mode).ConfigureAwait(true);
            _surface = surface;
            _preferences = preferences;
            SurfaceRegion region = ToRegion(preferences, surface);
            MacroPoint target = new(
                preferences.TargetX * surface.Width,
                preferences.TargetY * surface.Height);
            var clickSettings = new ClickMacroSettings(
                preferences.ClickCount,
                TimeSpan.FromSeconds(preferences.ClickIntervalSeconds));
            _setup.ShowSetup(
                surface,
                region,
                target,
                preferences.UseLargeFrame,
                preferences.Speed,
                clickSettings);
            CaptureSetupState();
            SetState(MacroSessionState.Configuring);
            SetStatus(_mode == MacroMode.Clicks
                ? "Ajuste a bolinha e pressione Play."
                : "Ajuste a moldura e pressione Play.");
            _setupTimer.Start();
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or IOException or System.ComponentModel.Win32Exception)
        {
            SetError($"Não foi possível abrir a configuração do macro: {exception.Message}");
        }
        catch (Exception exception)
        {
            // Unexpected failures must be visible in the workspace instead of leaving the
            // macro button without any response.
            SetError($"Falha inesperada ao abrir o macro: {exception.Message}");
        }
        finally
        {
            _openingSetup = false;
        }
    }

    private async Task ApplyModePreferencesAsync(MacroMode mode)
    {
        MacroProfilePreferences preferences = await LoadPreferencesAsync(mode).ConfigureAwait(true);
        if (_disposed || _mode != mode || !_surface.HasArea)
        {
            return;
        }

        _preferences = preferences;
        var clickSettings = new ClickMacroSettings(
            preferences.ClickCount,
            TimeSpan.FromSeconds(preferences.ClickIntervalSeconds));
        _setup.ShowSetup(
            _surface,
            ToRegion(preferences, _surface),
            new MacroPoint(
                preferences.TargetX * _surface.Width,
                preferences.TargetY * _surface.Height),
            preferences.UseLargeFrame,
            preferences.Speed,
            clickSettings);
        CaptureSetupState();
    }

    private async Task<MacroProfilePreferences> LoadPreferencesAsync(MacroMode mode)
    {
        if (_profilePreferences is null)
        {
            return _preferences;
        }

        try
        {
            ProfileMacroModes modes = await _profilePreferences
                .LoadAsync(_profileId)
                .ConfigureAwait(true);
            return modes.Get(mode);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            DiagnosticLog.Current.WriteFailure(
                "macro.preferences_load",
                "The macro profile preferences could not be loaded.",
                exception,
                ("profileId", _profileId.ToString()),
                ("mode", mode.ToString()));
            return MacroProfilePreferences.Default;
        }
    }

    private SurfaceRegion ToRegion(
        MacroProfilePreferences preferences,
        SurfaceGeometry surface)
    {
        if (!surface.HasArea)
        {
            return _frame;
        }

        int width = Math.Clamp(
            (int)Math.Round(preferences.AnalysisWidth * surface.Width),
            Math.Min(80, surface.Width),
            surface.Width);
        int height = Math.Clamp(
            (int)Math.Round(preferences.AnalysisHeight * surface.Height),
            Math.Min(80, surface.Height),
            surface.Height);
        int x = Math.Clamp(
            (int)Math.Round(preferences.AnalysisX * surface.Width),
            0,
            surface.Width - width);
        int y = Math.Clamp(
            (int)Math.Round(preferences.AnalysisY * surface.Height),
            0,
            surface.Height - height);
        return new SurfaceRegion(x, y, width, height);
    }

    private void CaptureSetupState()
    {
        if (!_surface.HasArea)
        {
            return;
        }

        _frame = _setup.Region;
        _target = _setup.Target;
        _useLargeFrame = _setup.UseLargeFrame;
        _speed = _setup.Speed;
        _preferences = _setup.GetProfilePreferences(_surface);
    }

    private void ScheduleSave()
    {
        if (_disposed)
        {
            return;
        }

        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private async void SaveTimerOnTick(object? sender, EventArgs eventArgs)
    {
        _saveTimer.Stop();
        await PersistPreferencesAsync().ConfigureAwait(true);
    }

    private Task PersistPreferencesAsync() =>
        PersistPreferencesAsync(_mode, _preferences);

    private async Task PersistPreferencesAsync(
        MacroMode mode,
        MacroProfilePreferences preferences)
    {
        if (_profilePreferences is null || _profileId == Guid.Empty)
        {
            return;
        }

        try
        {
            await _profilePreferences
                .SaveAsync(_profileId, mode, preferences)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            DiagnosticLog.Current.WriteFailure(
                "macro.preferences_save",
                "The macro profile preferences could not be saved.",
                exception,
                ("profileId", _profileId.ToString()),
                ("mode", mode.ToString()));
        }
    }

    private TimeSpan ScaleDelay(TimeSpan delay)
    {
        double speed = Math.Clamp(_speed, 0.25, 4.0);
        double milliseconds = delay.TotalMilliseconds / speed;
        return TimeSpan.FromMilliseconds(Math.Max(1, milliseconds));
    }

    private void SetupTimerOnTick(object? sender, EventArgs eventArgs)
    {
        if (_disposed || _state != MacroSessionState.Configuring)
        {
            return;
        }

        GameWindowAttachment? attachment = _session.Attachment;
        if (attachment is null)
        {
            SetError("A sessão perdeu a janela do jogo.");
            return;
        }

        if (!IsExecutionSurfaceAvailable(attachment))
        {
            Stop();
            return;
        }

        try
        {
            SurfaceGeometry surface = _capture.GetGeometry(attachment.GameWindow);
            if (!surface.HasArea)
            {
                _setup.HideSetup();
                return;
            }

            _setup.ApplySurface(surface);
            _surface = surface;
            CaptureSetupState();
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or IOException or System.ComponentModel.Win32Exception)
        {
            SetStatus($"Aguardando a janela do jogo: {exception.Message}");
        }
    }

    private void SetupOnRegionChanged(object? sender, SurfaceRegion region)
    {
        _frame = region;
        ScheduleSave();
    }

    private void SetupOnSettingsChanged(object? sender, EventArgs eventArgs)
    {
        CaptureSetupState();
        ScheduleSave();
    }

    private void SetupOnStopRequested(object? sender, EventArgs eventArgs) => Stop();

    private void SetupOnPlayRequested(object? sender, EventArgs eventArgs)
    {
        if (_state != MacroSessionState.Configuring)
        {
            return;
        }

        GameWindowAttachment? attachment = _session.Attachment;
        if (!IsExecutionSurfaceAvailable(attachment))
        {
            SetError("A sessão não está visível para executar o macro.");
            return;
        }

        CaptureSetupState();
        _clickSettings = _setup.GetClickSettings();
        _saveTimer.Stop();
        _ = PersistPreferencesAsync();
        if (!_frame.HasArea)
        {
            SetError("A área selecionada não tem tamanho válido.");
            return;
        }

        _setupTimer.Stop();
        _setup.HideSetup();
        BeginExecution();
    }

    private void BeginExecution()
    {
        if (_runTask is not null)
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        SetState(MacroSessionState.Starting);
        SetStatus("Iniciando motor visual...");
        Task runTask = Task.Run(() => RunAsync(cancellation));
        _runTask = runTask;
        _ = runTask.ContinueWith(
            completed =>
            {
                if (ReferenceEquals(_runTask, completed))
                {
                    _runTask = null;
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task RunClickLoopAsync(CancellationToken cancellationToken)
    {
        int performed = 0;
        while (!cancellationToken.IsCancellationRequested && _session.IsRunning)
        {
            if (_clickSettings.Count > 0 && performed >= _clickSettings.Count)
            {
                break;
            }

            GameWindowAttachment? attachment = _session.Attachment;
            if (attachment is null)
            {
                throw new InvalidOperationException("A sessão não possui uma superfície anexada.");
            }

            if (!IsExecutionSurfaceAvailable(attachment))
            {
                RequestStopFromWorker();
                return;
            }

            SurfaceGeometry geometry = _capture.GetGeometry(attachment.GameWindow);
            if (!geometry.HasArea)
            {
                RequestStopFromWorker();
                return;
            }

            SurfaceRegion frame = ToRegion(_preferences, geometry);
            if (!frame.HasArea)
            {
                throw new InvalidOperationException("A área do macro não é válida.");
            }

            // The placed aim is the reference: the macro always clicks exactly where the
            // crosshair was left, even when the analysis area is centered on it.
            MacroPoint target = new(
                Math.Clamp(
                    _preferences.TargetX * geometry.Width,
                    0,
                    Math.Max(0, geometry.Width - 1)),
                Math.Clamp(
                    _preferences.TargetY * geometry.Height,
                    0,
                    Math.Max(0, geometry.Height - 1)));

            var visualResult = new MacroScanResult(
                Ok: true,
                Found: true,
                Mode: MacroMode.Clicks,
                Move: null,
                Action: new MacroAction(
                    "click",
                    "Clique",
                    target,
                    null,
                    null,
                    null,
                    false,
                    TimeSpan.Zero),
                StableFrames: 0,
                Signature: string.Empty,
                Status: _clickSettings.IsInfinite
                    ? $"Cliques contínuos: {performed}"
                    : $"Cliques: {performed}/{_clickSettings.Count}",
                Error: null,
                Confidence: 1);
            Publish(geometry, visualResult);

            if (!IsExecutionSurfaceAvailable(attachment))
            {
                RequestStopFromWorker();
                return;
            }

            DirectInputResult input = _input.Click(_session, target, cancellationToken);
            if (!input.Succeeded)
            {
                throw new InvalidOperationException(input.Message);
            }

            performed++;
            if (_clickSettings.Count > 0 && performed >= _clickSettings.Count)
            {
                break;
            }

            SetStatus(_clickSettings.IsInfinite
                ? $"Cliques contínuos: {performed}"
                : $"Cliques: {performed}/{_clickSettings.Count}");
            await Task.Delay(ScaleDelay(_clickSettings.Interval), cancellationToken).ConfigureAwait(false);
        }

        if (!_clickSettings.IsInfinite)
        {
            SetStatus($"Cliques concluídos: {performed}");
        }
    }

    private async Task RunAsync(CancellationTokenSource cancellation)
    {
        CancellationToken cancellationToken = cancellation.Token;
        try
        {
            if (_mode == MacroMode.Clicks)
            {
                await RunClickLoopAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            await using var bridge = new GemMacroBridgeClient(_bridgeOptions);
            string? awaitingSignature = null;
            DateTimeOffset nextActionAt = DateTimeOffset.MinValue;

            while (!cancellationToken.IsCancellationRequested && _session.IsRunning)
            {
                cancellationToken.ThrowIfCancellationRequested();
                GameWindowAttachment? attachment = _session.Attachment;
                if (attachment is null)
                {
                    throw new InvalidOperationException("A sessão não possui uma superfície anexada.");
                }

                if (!IsExecutionSurfaceAvailable(attachment))
                {
                    RequestStopFromWorker();
                    return;
                }

                CapturedSurface surface;
                try
                {
                    surface = _capture.Capture(attachment.GameWindow);
                }
                catch (Exception exception) when (
                    exception is InvalidOperationException or
                    IOException or
                    System.ComponentModel.Win32Exception)
                {
                    SetStatus("Aguardando a superfície da sessão...");
                    await Task.Delay(250, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (!IsExecutionSurfaceAvailable(attachment))
                {
                    RequestStopFromWorker();
                    return;
                }

                SurfaceRegion frame = ToRegion(_preferences, surface.Geometry);
                if (!frame.HasArea)
                {
                    throw new InvalidOperationException("A área do macro não está válida.");
                }

                MacroScanResult localResult = await bridge.AnalyzeAsync(
                    surface.JpegBytes,
                    _mode,
                    frame,
                    cancellationToken).ConfigureAwait(false);
                if (!IsExecutionSurfaceAvailable(attachment))
                {
                    RequestStopFromWorker();
                    return;
                }

                MacroScanResult result = localResult.Offset(frame.X, frame.Y);
                Publish(surface.Geometry, result);

                if (!result.Ok)
                {
                    throw new InvalidOperationException(result.Error ?? "Falha no motor visual.");
                }

                DateTimeOffset now = DateTimeOffset.UtcNow;
                if (result.Mode == MacroMode.Gems)
                {
                    if (awaitingSignature is not null &&
                        !string.Equals(awaitingSignature, result.Signature, StringComparison.Ordinal))
                    {
                        awaitingSignature = null;
                    }

                    if (result.Move is { } move &&
                        result.StableFrames >= 2 &&
                        awaitingSignature is null &&
                        now >= nextActionAt)
                    {
                        if (!IsExecutionSurfaceAvailable(attachment))
                        {
                            RequestStopFromWorker();
                            return;
                        }

                        DirectInputResult input = _input.ExecuteMove(_session, move, cancellationToken);
                        if (!input.Succeeded)
                        {
                            throw new InvalidOperationException(input.Message);
                        }

                        awaitingSignature = result.Signature;
                        nextActionAt = now + ScaleDelay(BoardChangeGrace);
                        SetStatus("Troca enviada sem mover o mouse.");
                    }
                }
                else if (result.Action is { } action && now >= nextActionAt)
                {
                    if (!IsExecutionSurfaceAvailable(attachment))
                    {
                        RequestStopFromWorker();
                        return;
                    }

                    DirectInputResult input = _input.ExecuteAction(_session, action, cancellationToken);
                    if (!input.Succeeded)
                    {
                        throw new InvalidOperationException(input.Message);
                    }

                    nextActionAt = now + ScaleDelay(action.Cooldown);
                    SetStatus($"{action.Label} enviado sem mover o mouse.");
                }

                TimeSpan interval = ScaleDelay(
                    _mode == MacroMode.Cosmo ? CosmoScanInterval : GemsScanInterval);
                TimeSpan remaining = nextActionAt - DateTimeOffset.UtcNow;
                if (remaining > interval)
                {
                    interval = remaining;
                }

                await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or
            IOException or
            ArgumentException or
            NotSupportedException or
            EntryPointNotFoundException or
            System.ComponentModel.Win32Exception or
            System.Text.Json.JsonException)
        {
            SetError(exception.Message);
        }
        finally
        {
            cancellation.Dispose();
            if (ReferenceEquals(_cancellation, cancellation))
            {
                _cancellation = null;
            }

            _setupTimer.Stop();
            _ = _dispatcher.InvokeAsync(() =>
            {
                try
                {
                    _overlay.HideOverlay();
                }
                catch (InvalidOperationException)
                {
                }

                if (!_disposed && _state != MacroSessionState.Error)
                {
                    SetState(MacroSessionState.Stopped);
                    SetStatus("Parado");
                }
            });
        }
    }

    private void Publish(SurfaceGeometry geometry, MacroScanResult result)
    {
        _ = _dispatcher.InvokeAsync(() =>
        {
            if (_disposed)
            {
                return;
            }

            // Focus changes must NOT stop the macro or hide the crosshair: the user is
            // expected to leave the launcher working in the background. Only a hidden or
            // minimized launcher invalidates the click coordinates.
            if (!IsOwnerAvailable())
            {
                _overlay.HideOverlay();
                return;
            }

            SetState(MacroSessionState.Running);
            SetStatus(result.Status);
            try
            {
                _overlay.Apply(geometry, result, result.Status, _overlayVisible);
            }
            catch (InvalidOperationException)
            {
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }
        });
    }

    private void SetError(string message)
    {
        DiagnosticLog.Current.WriteFailure(
            "macro.session",
            "The macro session stopped after a runtime error.",
            exception: null,
            ("sessionId", _session.Id.ToString()),
            ("mode", _mode.ToString()),
            ("message", message));
        _setupTimer.Stop();
        _ = _dispatcher.InvokeAsync(() =>
        {
            if (_disposed)
            {
                return;
            }

            SetState(MacroSessionState.Error);
            SetStatus(message);
            try
            {
                _setup.HideSetup();
                _overlay.HideOverlay();
            }
            catch (InvalidOperationException)
            {
            }
        });
    }

    private void SetState(MacroSessionState state) => State = state;

    private void SetStatus(string status)
    {
        if (_dispatcher.CheckAccess())
        {
            StatusText = status;
            return;
        }

        _ = _dispatcher.InvokeAsync(() => StatusText = status);
    }

    private bool IsExecutionSurfaceAvailable(GameWindowAttachment? attachment)
    {
        if (_disposed || attachment is null || !IsOwnerAvailable())
        {
            return false;
        }

        nint gameWindow = attachment.GameWindow;
        return NativeWindowMethods.IsWindowHandle(gameWindow) &&
               NativeWindowMethods.IsWindowVisible(gameWindow) &&
               !NativeWindowMethods.IsWindowMinimized(gameWindow);
    }

    private bool IsOwnerAvailable()
    {
        if (_dispatcher.CheckAccess())
        {
            return _uiOwner.IsVisible && _uiOwner.WindowState != WindowState.Minimized;
        }

        try
        {
            return _dispatcher.Invoke(
                () => _uiOwner.IsVisible && _uiOwner.WindowState != WindowState.Minimized);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    private void RequestStopFromWorker() => CancelSafely(_cancellation);

    private static void CancelSafely(CancellationTokenSource? cancellation)
    {
        try
        {
            cancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void SessionOnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(GameSessionViewModel.IsRunning) && !_session.IsRunning)
        {
            Stop();
        }
    }
}

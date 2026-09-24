using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using LegendLauncher.App.GameHosting;
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
    private readonly Dispatcher _dispatcher;
    private readonly GameSurfaceCapture _capture = new();
    private readonly DirectGameInput _input = new();
    private readonly MacroOverlayWindow _overlay;
    private readonly MacroSetupWindow _setup;
    private readonly DispatcherTimer _setupTimer;
    private readonly MacroBridgeOptions _bridgeOptions;
    private CancellationTokenSource? _cancellation;
    private Task? _runTask;
    private ClickMacroSettings _clickSettings = new(0, TimeSpan.FromSeconds(0.10));
    private SurfaceRegion _frame;
    private MacroMode _mode = MacroMode.Gems;
    private MacroSessionState _state = MacroSessionState.Stopped;
    private string _statusText = "Parado";
    private bool _overlayVisible = true;
    private bool _disposed;

    public MacroSessionController(
        GameSessionViewModel session,
        Window uiOwner)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        ArgumentNullException.ThrowIfNull(uiOwner);
        _dispatcher = uiOwner.Dispatcher;
        _overlay = new MacroOverlayWindow(session.Id, uiOwner);
        _setup = new MacroSetupWindow(session.TabTitle, uiOwner);
        _setup.PlayRequested += SetupOnPlayRequested;
        _setup.StopRequested += SetupOnStopRequested;
        _setup.RegionChanged += SetupOnRegionChanged;
        _setupTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = SetupRefreshInterval,
        };
        _setupTimer.Tick += SetupTimerOnTick;
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
        _mode = mode;
        _setup.SetMode(mode);
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_runTask is not null || !_session.IsRunning)
        {
            return;
        }

        OpenSetup();
    }

    public void Stop()
    {
        CancelSafely(_cancellation);
        _setupTimer.Stop();
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
        _setup.PlayRequested -= SetupOnPlayRequested;
        _setup.StopRequested -= SetupOnStopRequested;
        _setup.RegionChanged -= SetupOnRegionChanged;
        _session.PropertyChanged -= SessionOnPropertyChanged;
        CancelSafely(_cancellation);
        _ = _dispatcher.InvokeAsync(() =>
        {
            _setup.Close();
            _overlay.Close();
        });
    }

    private void OpenSetup()
    {
        if (_state == MacroSessionState.Configuring)
        {
            return;
        }

        GameWindowAttachment? attachment = _session.Attachment;
        if (attachment is null)
        {
            SetError("A sessão não possui uma superfície anexada.");
            return;
        }

        try
        {
            SurfaceGeometry surface = _capture.GetGeometry(attachment.GameWindow);
            if (!surface.HasArea)
            {
                SetError("A superfície da sessão ainda não está visível.");
                return;
            }

            _setup.ShowSetup(surface, _frame);
            _frame = _setup.Region;
            SetState(MacroSessionState.Configuring);
            SetStatus("Ajuste a moldura e pressione Play.");
            _setupTimer.Start();
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or IOException or System.ComponentModel.Win32Exception)
        {
            SetError($"Não foi possível abrir a moldura: {exception.Message}");
        }
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

        try
        {
            SurfaceGeometry surface = _capture.GetGeometry(attachment.GameWindow);
            if (!surface.HasArea)
            {
                _setup.HideSetup();
                return;
            }

            _setup.ApplySurface(surface);
            _frame = _setup.Region;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or IOException or System.ComponentModel.Win32Exception)
        {
            SetStatus($"Aguardando a janela do jogo: {exception.Message}");
        }
    }

    private void SetupOnRegionChanged(object? sender, SurfaceRegion region) => _frame = region;

    private void SetupOnStopRequested(object? sender, EventArgs eventArgs) => Stop();

    private void SetupOnPlayRequested(object? sender, EventArgs eventArgs)
    {
        if (_state != MacroSessionState.Configuring)
        {
            return;
        }

        _frame = _setup.Region;
        _clickSettings = _setup.GetClickSettings();
        if (!_frame.HasArea)
        {
            SetError("A moldura selecionada não tem área válida.");
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
        if (!_frame.HasArea)
        {
            throw new InvalidOperationException("A moldura do macro não está válida.");
        }

        MacroPoint target = new(
            _frame.X + _frame.Width / 2.0,
            _frame.Y + _frame.Height / 2.0);
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

            SurfaceGeometry geometry = _capture.GetGeometry(attachment.GameWindow);
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
            await Task.Delay(_clickSettings.Interval, cancellationToken).ConfigureAwait(false);
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

                if (!_frame.HasArea)
                {
                    throw new InvalidOperationException("A moldura do macro não está válida.");
                }

                MacroScanResult localResult = await bridge.AnalyzeAsync(
                    surface.JpegBytes,
                    _mode,
                    _frame,
                    cancellationToken).ConfigureAwait(false);
                MacroScanResult result = localResult.Offset(_frame.X, _frame.Y);
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
                        DirectInputResult input = _input.ExecuteMove(_session, move, cancellationToken);
                        if (!input.Succeeded)
                        {
                            throw new InvalidOperationException(input.Message);
                        }

                        awaitingSignature = result.Signature;
                        nextActionAt = now + BoardChangeGrace;
                        SetStatus("Troca enviada sem mover o mouse.");
                    }
                }
                else if (result.Action is { } action && now >= nextActionAt)
                {
                    DirectInputResult input = _input.ExecuteAction(_session, action, cancellationToken);
                    if (!input.Succeeded)
                    {
                        throw new InvalidOperationException(input.Message);
                    }

                    nextActionAt = now + action.Cooldown;
                    SetStatus($"{action.Label} enviado sem mover o mouse.");
                }

                TimeSpan interval = _mode == MacroMode.Cosmo ? CosmoScanInterval : GemsScanInterval;
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

using LegendLauncher.Core.Models;

namespace LegendLauncher.GameHost.Legacy;

internal sealed class LegacyGameHostForm : Form
{
    private readonly LegacyRuntimeAssets _assets;
    private LaunchSession? _pendingSession;
    private GameRuntimeOptions? _runtimeOptions;
    private Action<bool, nint>? _handshakeCompletion;
    private RegistrationFreeActivationContext? _activationContext;
    private int _parentExitCloseRequested;
    private int _parentExitCloseQueued;

    public LegacyGameHostForm(LegacyRuntimeAssets assets)
    {
        _assets = assets ?? throw new ArgumentNullException(nameof(assets));
        ConfigureWindow($"Urus GameHost · {GameHostLocalization.Get(GameHostText.CompatibilityProbe)}");
        Controls.Add(BuildDiagnosticContent(assets));
    }

    public LegacyGameHostForm(
        LegacyRuntimeAssets assets,
        LaunchSession session,
        GameRuntimeOptions runtimeOptions,
        Action<bool, nint> handshakeCompletion)
    {
        _assets = assets ?? throw new ArgumentNullException(nameof(assets));
        _pendingSession = session ?? throw new ArgumentNullException(nameof(session));
        _runtimeOptions = runtimeOptions ?? throw new ArgumentNullException(nameof(runtimeOptions));
        _handshakeCompletion = handshakeCompletion ??
            throw new ArgumentNullException(nameof(handshakeCompletion));
        ConfigureWindow("Legend Online");
        ConfigureEmbeddedSessionWindow();
        Controls.Add(BuildLoadingContent());
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        if (Volatile.Read(ref _parentExitCloseRequested) != 0 ||
            _pendingSession is null ||
            _runtimeOptions is null)
        {
            return;
        }

        try
        {
            LegacyLaunchUriPolicy.EnsureAllowed(_pendingSession.LaunchUri);
            _activationContext = RegistrationFreeActivationContext.Activate(_assets);
            var flashControl = new FlashActiveXControl();
            Controls.Clear();
            Controls.Add(flashControl);
            flashControl.InitializeAndLoad(_pendingSession, _runtimeOptions);
            CompleteHandshake(isLoaded: true, nativeWindowHandle: Handle);
        }
        catch (Exception)
        {
            TryCompleteHandshake(isLoaded: false, nativeWindowHandle: nint.Zero);
            _activationContext?.Dispose();
            _activationContext = null;
            MessageBox.Show(
                GameHostLocalization.Get(GameHostText.FlashInitializationFailed),
                "Urus GameHost",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            BeginInvoke((Action)Close);
        }
        finally
        {
            _pendingSession = null;
            _runtimeOptions = null;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        TryQueueParentExitClose();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        TryCompleteHandshake(isLoaded: false, nativeWindowHandle: nint.Zero);
        _activationContext?.Dispose();
        _activationContext = null;
        base.OnFormClosed(e);
    }

    internal void RequestCloseBecauseParentExited()
    {
        Interlocked.Exchange(ref _parentExitCloseRequested, 1);
        TryQueueParentExitClose();
    }

    private void CompleteHandshake(bool isLoaded, nint nativeWindowHandle)
    {
        Action<bool, nint>? completion = Interlocked.Exchange(ref _handshakeCompletion, null);
        completion?.Invoke(isLoaded, nativeWindowHandle);
    }

    private void TryCompleteHandshake(bool isLoaded, nint nativeWindowHandle)
    {
        try
        {
            CompleteHandshake(isLoaded, nativeWindowHandle);
        }
        catch (Exception)
        {
            // The launcher may already have timed out; the form will close safely.
        }
    }

    private void TryQueueParentExitClose()
    {
        if (Volatile.Read(ref _parentExitCloseRequested) == 0 ||
            IsDisposed ||
            Disposing ||
            !IsHandleCreated ||
            Interlocked.CompareExchange(ref _parentExitCloseQueued, 1, 0) != 0)
        {
            return;
        }

        try
        {
            BeginInvoke((Action)CloseAfterParentExit);
        }
        catch (InvalidOperationException)
        {
            // The handle may have been destroyed between IsHandleCreated and
            // BeginInvoke. A future OnHandleCreated call can safely try again.
            Interlocked.Exchange(ref _parentExitCloseQueued, 0);
        }
    }

    private void CloseAfterParentExit()
    {
        if (!IsDisposed && !Disposing)
        {
            Close();
        }
    }

    private void ConfigureWindow(string title)
    {
        Text = title;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 480);
        ClientSize = new Size(980, 620);
        BackColor = Color.FromArgb(8, 13, 24);
        ForeColor = Color.FromArgb(244, 247, 251);
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
    }

    private void ConfigureEmbeddedSessionWindow()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        ControlBox = false;
        MinimizeBox = false;
        MaximizeBox = false;
    }

    private static Control BuildLoadingContent() => new Label
    {
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter,
        Text = GameHostLocalization.Get(GameHostText.PreparingIsolatedEnvironment),
        ForeColor = Color.FromArgb(170, 182, 200),
    };

    private static Control BuildDiagnosticContent(LegacyRuntimeAssets assets)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(36),
            ColumnCount = 1,
            RowCount = 5,
            BackColor = Color.Transparent,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label
        {
            AutoSize = true,
            Text = GameHostLocalization.Get(GameHostText.IsolatedGameHost),
            Font = new Font("Segoe UI", 24F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(244, 247, 251),
        }, 0, 0);
        layout.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(860, 0),
            Text = GameHostLocalization.Get(GameHostText.DiagnosticModeDescription),
            ForeColor = Color.FromArgb(170, 182, 200),
        }, 0, 2);

        var status = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(17, 24, 39),
            Padding = new Padding(20),
        };
        status.Controls.Add(BuildStatusLabel(assets));
        layout.Controls.Add(status, 0, 4);
        return layout;
    }

    private static Label BuildStatusLabel(LegacyRuntimeAssets assets)
    {
        string state = assets.IsComplete
            ? $"{GameHostLocalization.Get(GameHostText.CompatibilityFound)}\r\n\r\n" +
              GameHostLocalization.Get(GameHostText.CompatibilityFoundDetail)
            : $"{GameHostLocalization.Get(GameHostText.CompatibilityIncomplete)}\r\n\r\n" +
              $"{GameHostLocalization.Get(GameHostText.MissingLabel)}: " +
              string.Join(", ", assets.MissingFiles);

        return new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Text = state + $"\r\n\r\n{GameHostLocalization.Get(GameHostText.VerifiedFolderLabel)}:\r\n" +
                assets.RuntimeRoot,
            ForeColor = assets.IsComplete ? Color.FromArgb(88, 214, 141) : Color.FromArgb(245, 185, 90),
        };
    }
}

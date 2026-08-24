namespace LegendLauncher.GameHost.Legacy;

/// <summary>
/// Keeps a top-level HWND in the GameHost process so memory tools can find Flash
/// after the session form is reparented. The window stays off-screen and invisible.
/// </summary>
internal sealed class GameHostProcessAnchorForm : Form
{
    internal const int ExtendedStyleAppWindow = 0x00040000;
    internal const int ExtendedStyleToolWindow = 0x00000080;
    private const int ExtendedStyleNoActivate = 0x08000000;
    private const int OffScreenCoordinate = -32000;
    private const int WmSysCommand = 0x0112;
    private const int SystemCommandRestore = 0xF120;
    private const int SystemCommandMaximize = 0xF030;
    private bool _hidingFromUser;

    internal GameHostProcessAnchorForm()
    {
        Text = GameHostLocalization.Get(GameHostText.ProcessAnchorTitle);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        ShowInTaskbar = false;
        ShowIcon = false;
        ControlBox = false;
        MinimizeBox = false;
        MaximizeBox = false;
        StartPosition = FormStartPosition.Manual;
        MinimumSize = new Size(1, 1);
        MaximumSize = new Size(1, 1);
        HideFromUser();
    }

    internal bool IsEnumeratedByExternalWindowTools =>
        FormBorderStyle != FormBorderStyle.None &&
        (CreateParams.ExStyle & ExtendedStyleAppWindow) != 0 &&
        (CreateParams.ExStyle & ExtendedStyleToolWindow) == 0;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams createParams = base.CreateParams;
            createParams.ExStyle |= ExtendedStyleAppWindow | ExtendedStyleNoActivate;
            createParams.ExStyle &= ~ExtendedStyleToolWindow;
            return createParams;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        HideFromUser();
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        HideFromUser();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        HideFromUser();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideFromUser();
            return;
        }

        base.OnFormClosing(e);
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmSysCommand)
        {
            int command = message.WParam.ToInt32() & 0xFFF0;
            if (command is SystemCommandRestore or SystemCommandMaximize)
            {
                HideFromUser();
                return;
            }
        }

        base.WndProc(ref message);
    }

    internal static GameHostProcessAnchorForm Start()
    {
        var form = new GameHostProcessAnchorForm();
        form.Show();
        form.HideFromUser();
        return form;
    }

    private void HideFromUser()
    {
        if (_hidingFromUser)
        {
            return;
        }

        _hidingFromUser = true;
        try
        {
            Opacity = 0;
            WindowState = FormWindowState.Minimized;
            Location = new Point(OffScreenCoordinate, OffScreenCoordinate);
            ClientSize = new Size(1, 1);
        }
        finally
        {
            _hidingFromUser = false;
        }
    }
}

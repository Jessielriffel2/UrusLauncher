namespace LegendLauncher.GameHost.Legacy;

/// <summary>
/// Keeps a top-level, taskbar-visible window in the GameHost process so memory tools
/// can find the Flash process after the session form is reparented into the launcher.
/// </summary>
internal sealed class GameHostProcessAnchorForm : Form
{
    internal const int ExtendedStyleAppWindow = 0x00040000;
    internal const int ExtendedStyleToolWindow = 0x00000080;

    internal GameHostProcessAnchorForm()
    {
        Text = GameHostLocalization.Get(GameHostText.ProcessAnchorTitle);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        ShowInTaskbar = true;
        ShowIcon = true;
        ControlBox = true;
        MinimizeBox = true;
        MaximizeBox = false;
        StartPosition = FormStartPosition.Manual;
        ClientSize = new Size(420, 96);
        MinimumSize = new Size(360, 120);
        MaximumSize = new Size(640, 180);
        BackColor = Color.FromArgb(8, 13, 24);
        ForeColor = Color.FromArgb(244, 247, 251);
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        Padding = new Padding(16);
        Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Text = GameHostLocalization.Get(GameHostText.ProcessAnchorDescription),
            ForeColor = Color.FromArgb(170, 182, 200),
        });
    }

    internal bool IsEnumeratedByExternalWindowTools =>
        ShowInTaskbar &&
        FormBorderStyle != FormBorderStyle.None &&
        (CreateParams.ExStyle & ExtendedStyleAppWindow) != 0 &&
        (CreateParams.ExStyle & ExtendedStyleToolWindow) == 0;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams createParams = base.CreateParams;
            createParams.ExStyle |= ExtendedStyleAppWindow;
            createParams.ExStyle &= ~ExtendedStyleToolWindow;
            return createParams;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        WindowState = FormWindowState.Minimized;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            WindowState = FormWindowState.Minimized;
            return;
        }

        base.OnFormClosing(e);
    }

    internal static GameHostProcessAnchorForm Start()
    {
        var form = new GameHostProcessAnchorForm();
        form.Show();
        return form;
    }
}

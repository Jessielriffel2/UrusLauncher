using System.ComponentModel;
using LegendLauncher.Core.Models;

namespace LegendLauncher.GameHost.Legacy;

/// <summary>
/// Registration-free wrapper for the Flash ActiveX control in the isolated process.
/// </summary>
internal sealed class FlashActiveXControl : AxHost
{
    private const string FlashClassId = "{D27CDB6E-AE6D-11CF-96B8-444553540000}";
    private const int WindowMessagePrint = 0x0317;
    private const int WindowMessagePrintClient = 0x0318;

    // How often the launcher's zoom choice and the monitor scale are re-read (the change is applied live).
    private const int ZoomRefreshMilliseconds = 300;

    private System.Windows.Forms.Timer? _zoomRefresh;
    private bool _zoomInstalled;

    public FlashActiveXControl()
        : base(FlashClassId)
    {
        Dock = DockStyle.Fill;
    }

    public void InitializeAndLoad(LaunchSession session, GameRuntimeOptions options, string? flashOcxPath = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(options);
        LegacyLaunchUriPolicy.EnsureAllowed(session.LaunchUri);

        ISupportInitialize initializer = this;
        initializer.BeginInit();
        initializer.EndInit();
        dynamic flash = GetOcx() ??
            throw new InvalidOperationException("Flash ActiveX initialization returned no COM object.");

        // The OCX is loaded now: keep its cursor/capture calls from leaking into the launcher before the
        // movie starts handling the mouse.
        if (flashOcxPath is not null)
        {
            FlashPointerIsolation.Install(flashOcxPath);
            // The launcher's colors and zoom act on the GDI copies of the Compatible mode (opaque) only. In the DirectX mode
            // (direct) Flash presents through Direct3D without those calls, so their hooks stay out (they would only add work
            // to every internal copy) and the launcher greys both controls out.
            if (options.WindowMode == GameWindowMode.Opaque)
            {
                // color saturation slider (frames shown on the window only)
                FlashColorFilter.Install(flashOcxPath);
                // game zoom
                FlashDisplayScale.Install(flashOcxPath, Handle, (object)flash);
                _zoomInstalled = true;
                ApplyZoom(force: true);
                StartZoomRefresh();
            }
        }

        FlashRuntimeConfiguration configuration = FlashRuntimeConfiguration.From(options);

        configuration.ApplyTo(flash);
        flash.FlashVars = FlashSessionParameters.Encode(session.Parameters);

        // Validate again at the last possible point before COM receives the address.
        LegacyLaunchUriPolicy.EnsureAllowed(session.LaunchUri);
        flash.LoadMovie(0, session.LaunchUri.AbsoluteUri);
    }

    /// <summary>
    /// Messages on their way to Flash's own window procedure (WinForms already used the originals): with the game zoom the mouse
    /// coordinates become game pixels, and a PrintWindow capture is enlarged like the screen.
    /// </summary>
    protected override void DefWndProc(ref Message m)
    {
        if (!_zoomInstalled)
        {
            base.DefWndProc(ref m);
            return;
        }

        if (m.Msg is WindowMessagePrint or WindowMessagePrintClient)
        {
            nint previous = FlashDisplayScale.BeginPrint(m.WParam);
            try
            {
                base.DefWndProc(ref m);
            }
            finally
            {
                FlashDisplayScale.EndPrint(previous);
            }

            return;
        }

        nint translated = FlashDisplayScale.TranslateMessageParameter(m.Msg, m.LParam);
        if (translated == m.LParam)
        {
            base.DefWndProc(ref m);
            return;
        }

        Message forFlash = Message.Create(m.HWnd, m.Msg, m.WParam, translated);
        base.DefWndProc(ref forFlash);
        m.Result = forFlash.Result;
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        ApplyZoom(force: false);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _zoomRefresh?.Dispose();
            _zoomRefresh = null;
        }

        base.Dispose(disposing);
    }

    private void StartZoomRefresh()
    {
        _zoomRefresh = new System.Windows.Forms.Timer { Interval = ZoomRefreshMilliseconds };
        _zoomRefresh.Tick += (_, _) => ApplyZoom(force: false);
        _zoomRefresh.Start();
    }

    private void ApplyZoom(bool force)
    {
        if (_zoomInstalled && IsHandleCreated && !IsDisposed)
        {
            FlashDisplayScale.Refresh(Left, Top, Width, Height, force);
        }
    }
}

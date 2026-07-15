using System.ComponentModel;
using LegendLauncher.Core.Models;

namespace LegendLauncher.GameHost.Legacy;

/// <summary>
/// Registration-free wrapper for the Flash ActiveX control in the isolated process.
/// </summary>
internal sealed class FlashActiveXControl : AxHost
{
    private const string FlashClassId = "{D27CDB6E-AE6D-11CF-96B8-444553540000}";

    public FlashActiveXControl()
        : base(FlashClassId)
    {
        Dock = DockStyle.Fill;
    }

    public void InitializeAndLoad(LaunchSession session, GameRuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(options);
        LegacyLaunchUriPolicy.EnsureAllowed(session.LaunchUri);

        ISupportInitialize initializer = this;
        initializer.BeginInit();
        initializer.EndInit();
        dynamic flash = GetOcx() ??
            throw new InvalidOperationException("Flash ActiveX initialization returned no COM object.");
        FlashRuntimeConfiguration configuration = FlashRuntimeConfiguration.From(options);

        configuration.ApplyTo(flash);
        flash.FlashVars = FlashSessionParameters.Encode(session.Parameters);

        // Validate again at the last possible point before COM receives the address.
        LegacyLaunchUriPolicy.EnsureAllowed(session.LaunchUri);
        flash.LoadMovie(0, session.LaunchUri.AbsoluteUri);
    }
}

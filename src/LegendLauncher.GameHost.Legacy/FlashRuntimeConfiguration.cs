using LegendLauncher.Core.Models;

namespace LegendLauncher.GameHost.Legacy;

internal sealed record FlashRuntimeConfiguration(
    string AllowScriptAccess,
    string AllowFullScreen,
    string AllowFullScreenInteractive,
    string Scale,
    string Quality,
    string WindowMode)
{
    public void ApplyTo(object flashControl)
    {
        ArgumentNullException.ThrowIfNull(flashControl);
        dynamic flash = flashControl;
        flash.AllowScriptAccess = AllowScriptAccess;
        flash.AllowFullScreen = AllowFullScreen;
        flash.AllowFullScreenInteractive = AllowFullScreenInteractive;
        flash.Scale = Scale;
        flash.Quality2 = Quality;
        flash.WMode = WindowMode;
    }

    public static FlashRuntimeConfiguration From(GameRuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new FlashRuntimeConfiguration(
            AllowScriptAccess: "sameDomain",
            AllowFullScreen: "false",
            AllowFullScreenInteractive: "false",
            Scale: "ShowAll",
            Quality: options.Quality switch
            {
                GameRenderQuality.Low => "low",
                GameRenderQuality.AutoLow => "autolow",
                GameRenderQuality.AutoHigh => "autohigh",
                GameRenderQuality.High => "high",
                _ => throw new ArgumentOutOfRangeException(nameof(options)),
            },
            WindowMode: options.WindowMode switch
            {
                GameWindowMode.Opaque => "opaque",
                GameWindowMode.Direct => "direct",
                _ => throw new ArgumentOutOfRangeException(nameof(options)),
            });
    }
}

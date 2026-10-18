using System.ComponentModel;
using System.Diagnostics;
using LegendLauncher.Providers.SwordOnline;

namespace LegendLauncher.App.ViewModels;

/// <summary>RECARGA button: opens the diamond top-up page of the SwordOnline website in the player's browser.</summary>
internal sealed partial class MainWindowViewModel
{
    private RelayCommand? _openRechargeCommand;

    public RelayCommand OpenRechargeCommand => _openRechargeCommand ??= new RelayCommand(OpenRecharge);

    public static Uri RechargeUri => SwordOnlinePlatformCatalog.RechargeUri;

    /// <summary>Opens a web address in the default browser (replaceable in tests).</summary>
    internal Action<Uri> ExternalLinkOpener { get; set; } = OpenInDefaultBrowser;

    private void OpenRecharge()
    {
        try
        {
            ExternalLinkOpener(RechargeUri);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            LogFailure(
                "recharge.open_failed",
                "The recharge page could not be opened in the browser.",
                exception,
                ("uri", RechargeUri.ToString()));
            SetStatusMessage("Recharge_OpenFailed", RechargeUri.ToString());
        }
    }

    private static void OpenInDefaultBrowser(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Only https pages are opened.");
        }

        using Process? _ = Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }
}

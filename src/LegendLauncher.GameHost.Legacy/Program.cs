using LegendLauncher.Core.Models;

namespace LegendLauncher.GameHost.Legacy;

internal static class Program
{
    private static readonly TimeSpan SessionReceiveTimeout = TimeSpan.FromSeconds(15);

    [STAThread]
    private static int Main(string[] args)
    {
        GameHostLocalization.InitializeFromEnvironment();
        ApplicationConfiguration.Initialize();

        if (!GameHostOptions.TryParse(args, out GameHostOptions? options, out _) || options is null)
        {
            MessageBox.Show(
                GameHostLocalization.Get(GameHostText.InvalidOptions),
                "Urus GameHost",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 2;
        }

        LegacyRuntimeAssets assets = LegacyRuntimeAssets.Discover(options.RuntimeRoot);
        if (options.DiagnosticsOnly)
        {
            Application.Run(new LegacyGameHostForm(assets));
            return 0;
        }

        if (!assets.IsComplete)
        {
            MessageBox.Show(
                GameHostLocalization.Get(GameHostText.FlashRuntimeIncomplete),
                "Urus GameHost",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 3;
        }

        try
        {
            var connection = LaunchSessionPipeClient
                .ConnectAsync(
                    options.PipeName!,
                    options.Nonce!,
                    options.ParentProcessId!.Value,
                    SessionReceiveTimeout)
                .GetAwaiter()
                .GetResult();
            try
            {
                RunSession(assets, options, connection);
            }
            finally
            {
                connection.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or
            InvalidOperationException or OperationCanceledException)
        {
            MessageBox.Show(
                GameHostLocalization.Get(GameHostText.LocalSessionInvalid),
                "Urus GameHost",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 4;
        }

        return 0;
    }

    private static void CompleteHandshake(
        LaunchSessionPipeConnection connection,
        bool isLoaded,
        nint nativeWindowHandle)
    {
        using var timeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        connection
            .CompleteAsync(isLoaded, nativeWindowHandle, timeoutSource.Token)
            .GetAwaiter()
            .GetResult();
    }

    private static void RunSession(
        LegacyRuntimeAssets assets,
        GameHostOptions options,
        LaunchSessionPipeConnection connection)
    {
        LaunchSession? session = connection.TakeSession();
        LegacyLaunchUriPolicy.EnsureAllowed(session.LaunchUri);
        using var form = new LegacyGameHostForm(
            assets,
            session,
            options.RuntimeOptions,
            (isLoaded, nativeWindowHandle) =>
                CompleteHandshake(connection, isLoaded, nativeWindowHandle));
        using var parentLifetimeMonitor = new ParentProcessLifetimeMonitor(
            options.ParentProcessId!.Value,
            form.RequestCloseBecauseParentExited);
        session = null;
        Application.Run(form);
    }
}

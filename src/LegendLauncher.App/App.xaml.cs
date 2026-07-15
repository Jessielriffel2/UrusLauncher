using System.Diagnostics;
using System.Windows;
using System.IO;
using System.Runtime.InteropServices;
using LegendLauncher.App.Localization;
using LegendLauncher.App.Services;
using LegendLauncher.Infrastructure.Paths;

namespace LegendLauncher.App;

public partial class App : Application
{
    private const string SingleInstanceMutexName = @"Local\UrusLauncher.App.SingleInstance";
    private const int RestoreWindowCommand = 9;
    private Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs eventArgs)
    {
        _singleInstanceMutex = new Mutex(
            initiallyOwned: true,
            SingleInstanceMutexName,
            out bool isFirstInstance);
        if (!isFirstInstance)
        {
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            TryActivateExistingInstance();
            Shutdown();
            return;
        }

        LocalizationService localization = LocalizationService.Current;
        try
        {
            var paths = new AppPaths();
            paths.EnsureDirectories();
            var settings = new LauncherSettingsService(paths.SettingsFile);
            LauncherSettingsSnapshot snapshot = settings.LoadAsync().GetAwaiter().GetResult();
            localization.SetLanguage(snapshot.LanguageCode);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            localization.SetLanguage(LocalizationService.DefaultLanguageCode);
        }

        localization.EnableThreadCultureUpdates();
        base.OnStartup(eventArgs);
    }

    protected override void OnExit(ExitEventArgs eventArgs)
    {
        if (_singleInstanceMutex is not null)
        {
            _singleInstanceMutex.ReleaseMutex();
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
        }

        base.OnExit(eventArgs);
    }

    private static void TryActivateExistingInstance()
    {
        using Process current = Process.GetCurrentProcess();
        string? currentExecutable = TryGetExecutablePath(current);
        if (currentExecutable is null)
        {
            return;
        }

        foreach (Process candidate in Process.GetProcessesByName(current.ProcessName))
        {
            using (candidate)
            {
                if (candidate.Id == current.Id ||
                    !IsMatchingInstance(candidate, current.SessionId, currentExecutable))
                {
                    continue;
                }

                nint window;
                try
                {
                    window = candidate.MainWindowHandle;
                }
                catch (InvalidOperationException)
                {
                    continue;
                }

                if (window == nint.Zero)
                {
                    continue;
                }

                if (IsIconic(window))
                {
                    _ = ShowWindowAsync(window, RestoreWindowCommand);
                }

                _ = SetForegroundWindow(window);
                return;
            }
        }
    }

    private static bool IsMatchingInstance(
        Process candidate,
        int currentSessionId,
        string currentExecutable)
    {
        try
        {
            return candidate.SessionId == currentSessionId &&
                string.Equals(
                    TryGetExecutablePath(candidate),
                    currentExecutable,
                    StringComparison.OrdinalIgnoreCase);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static string? TryGetExecutablePath(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch (Exception exception) when (
            exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return null;
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindowAsync(nint window, int showCommand);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint window);
}

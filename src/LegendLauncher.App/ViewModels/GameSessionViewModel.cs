using System.Diagnostics;
using System.Windows;
using LegendLauncher.App.GameHosting;
using LegendLauncher.Core.Models;

namespace LegendLauncher.App.ViewModels;

internal sealed class GameSessionViewModel : ObservableObject, IDisposable
{
    private readonly Process? _process;
    private bool _isDetached;
    private bool _isRunning = true;
    private bool _isSelected;
    private bool _disposed;

    public GameSessionViewModel(
        AccountProfile profile,
        PlatformDefinition platform,
        GameServer server,
        GameSession session,
        GameWindowAttachment? attachment)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(session);

        Id = Guid.NewGuid();
        ProfileId = profile.Id;
        ProfileName = profile.DisplayName;
        PlatformName = platform.DisplayName;
        ServerName = server.Name;
        ServerCode = server.Code;
        ProcessId = session.ProcessId;
        NativeWindowHandle = session.NativeWindowHandle;
        StartedAtUtc = session.StartedAtUtc;
        Attachment = attachment;

        try
        {
            _process = Process.GetProcessById(ProcessId);
            _process.EnableRaisingEvents = true;
            _process.Exited += ProcessOnExited;
            if (_process.HasExited)
            {
                _isRunning = false;
            }
        }
        catch (ArgumentException)
        {
            // Tests and a process that exits immediately may not have a live Process object.
            _isRunning = attachment is null;
        }
        catch (InvalidOperationException)
        {
            _isRunning = false;
        }
    }

    public event EventHandler? Exited;

    public Guid Id { get; }

    public Guid ProfileId { get; }

    public string ProfileName { get; }

    public string PlatformName { get; }

    public string ServerName { get; }

    public string ServerCode { get; }

    public int ProcessId { get; }

    public nint NativeWindowHandle { get; }

    public DateTimeOffset StartedAtUtc { get; }

    public GameWindowAttachment? Attachment { get; }

    public string Initial => string.IsNullOrWhiteSpace(ProfileName)
        ? "?"
        : ProfileName.Trim()[..1].ToUpperInvariant();

    public string TabTitle => $"{ProfileName} · {ServerCode}";

    public string SurfaceTitle => $"{ProfileName} · {PlatformName} · {ServerCode}";

    public string DetachedTitle => $"{ProfileName} · {ServerCode} {ServerName}";

    public bool IsDetached
    {
        get => _isDetached;
        internal set => SetProperty(ref _isDetached, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set => SetProperty(ref _isRunning, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        internal set => SetProperty(ref _isSelected, value);
    }

    public void Terminate()
    {
        if (_process is null)
        {
            return;
        }

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process already exited.
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Session removal must remain local even if Windows refuses termination.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_process is not null)
        {
            _process.Exited -= ProcessOnExited;
            _process.Dispose();
        }
    }

    private void ProcessOnExited(object? sender, EventArgs eventArgs)
    {
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            _ = dispatcher.BeginInvoke(NotifyExited);
            return;
        }

        NotifyExited();
    }

    private void NotifyExited()
    {
        if (_disposed || !IsRunning)
        {
            return;
        }

        IsRunning = false;
        Exited?.Invoke(this, EventArgs.Empty);
    }
}

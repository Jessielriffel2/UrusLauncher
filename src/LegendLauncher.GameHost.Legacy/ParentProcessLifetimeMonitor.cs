using System.ComponentModel;

namespace LegendLauncher.GameHost.Legacy;

internal sealed class ParentProcessLifetimeMonitor : IDisposable
{
    private readonly object _syncRoot = new();
    private Action? _parentExited;
    private IParentProcessExitSource? _exitSource;
    private bool _hasSignaled;
    private bool _isDisposed;

    public ParentProcessLifetimeMonitor(int parentProcessId, Action parentExited)
        : this(
            parentProcessId,
            parentExited,
            static processId => ParentProcessExitSource.Open(processId))
    {
    }

    internal ParentProcessLifetimeMonitor(
        int parentProcessId,
        Action parentExited,
        Func<int, IParentProcessExitSource> exitSourceFactory)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(parentProcessId);
        ArgumentNullException.ThrowIfNull(parentExited);
        ArgumentNullException.ThrowIfNull(exitSourceFactory);

        _parentExited = parentExited;
        IParentProcessExitSource? exitSource = null;
        try
        {
            exitSource = exitSourceFactory(parentProcessId) ??
                throw new InvalidOperationException("The parent process could not be observed.");
            _exitSource = exitSource;
            exitSource.Exited += ExitSourceOnExited;
            exitSource.StartWatching();

            // EnableRaisingEvents and HasExited intentionally overlap. If the process
            // exits between them, either path may signal and SignalParentExited keeps
            // the notification idempotent.
            if (exitSource.HasExited)
            {
                SignalParentExited();
            }
        }
        catch (Exception exception) when (IsUnavailableProcessFailure(exception))
        {
            ReleaseExitSource(exitSource);
            SignalParentExited();
        }
    }

    public void Dispose()
    {
        IParentProcessExitSource? exitSource;
        lock (_syncRoot)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _parentExited = null;
            exitSource = _exitSource;
            _exitSource = null;
        }

        ReleaseExitSource(exitSource);
    }

    private static bool IsUnavailableProcessFailure(Exception exception) =>
        exception is ArgumentException or InvalidOperationException or
            Win32Exception or NotSupportedException;

    private void ExitSourceOnExited(object? sender, EventArgs e)
    {
        SignalParentExited();
    }

    private void SignalParentExited()
    {
        lock (_syncRoot)
        {
            if (_isDisposed || _hasSignaled)
            {
                return;
            }

            _hasSignaled = true;
            Action? parentExited = _parentExited;
            _parentExited = null;
            parentExited?.Invoke();
        }
    }

    private void ReleaseExitSource(IParentProcessExitSource? exitSource)
    {
        if (exitSource is null)
        {
            return;
        }

        exitSource.Exited -= ExitSourceOnExited;
        exitSource.Dispose();

        lock (_syncRoot)
        {
            if (ReferenceEquals(_exitSource, exitSource))
            {
                _exitSource = null;
            }
        }
    }
}

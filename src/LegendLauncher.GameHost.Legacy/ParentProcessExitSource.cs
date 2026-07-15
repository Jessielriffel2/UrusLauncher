using System.Diagnostics;

namespace LegendLauncher.GameHost.Legacy;

internal interface IParentProcessExitSource : IDisposable
{
    event EventHandler? Exited;

    bool HasExited { get; }

    void StartWatching();
}

internal sealed class ParentProcessExitSource : IParentProcessExitSource
{
    private readonly Process _process;

    private ParentProcessExitSource(Process process)
    {
        _process = process;
    }

    public event EventHandler? Exited
    {
        add => _process.Exited += value;
        remove => _process.Exited -= value;
    }

    public bool HasExited => _process.HasExited;

    public static ParentProcessExitSource Open(int processId) =>
        new(Process.GetProcessById(processId));

    public void StartWatching()
    {
        _process.EnableRaisingEvents = true;
    }

    public void Dispose()
    {
        _process.Dispose();
    }
}

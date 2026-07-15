using System.Diagnostics;

namespace LegendLauncher.App.Updates;

internal interface IUpdateProcessStarter
{
    void Start(ProcessStartInfo startInfo);
}

internal sealed class UpdateProcessStarter : IUpdateProcessStarter
{
    public void Start(ProcessStartInfo startInfo)
    {
        _ = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The update installer could not be started.");
    }
}

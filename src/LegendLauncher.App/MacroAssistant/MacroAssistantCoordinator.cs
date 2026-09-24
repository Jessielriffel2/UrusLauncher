using System.Collections.Specialized;
using System.Windows;
using LegendLauncher.App.ViewModels;
using LegendLauncher.Infrastructure.Logging;

namespace LegendLauncher.App.MacroAssistant;

internal sealed class MacroAssistantCoordinator : IDisposable
{
    private readonly GameWorkspaceViewModel _workspace;
    private readonly Window _owner;
    private readonly Dictionary<Guid, MacroSessionController> _controllers = [];
    private bool _disposed;

    public MacroAssistantCoordinator(GameWorkspaceViewModel workspace, Window owner)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _workspace.Sessions.CollectionChanged += SessionsOnCollectionChanged;
        foreach (GameSessionViewModel session in _workspace.Sessions)
        {
            AddController(session);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _workspace.Sessions.CollectionChanged -= SessionsOnCollectionChanged;
        foreach (MacroSessionController controller in _controllers.Values.ToArray())
        {
            _workspace.MacroAssistant.RemoveController(controller);
        }

        _controllers.Clear();
    }

    private void SessionsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs)
    {
        if (_disposed)
        {
            return;
        }

        switch (eventArgs.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (eventArgs.NewItems?.OfType<GameSessionViewModel>() is { } added)
                {
                    foreach (GameSessionViewModel session in added)
                    {
                        AddController(session);
                    }
                }

                break;
            case NotifyCollectionChangedAction.Remove:
                if (eventArgs.OldItems?.OfType<GameSessionViewModel>() is { } removed)
                {
                    foreach (GameSessionViewModel session in removed)
                    {
                        RemoveController(session);
                    }
                }

                break;
            case NotifyCollectionChangedAction.Replace:
                if (eventArgs.OldItems?.OfType<GameSessionViewModel>() is { } replaced)
                {
                    foreach (GameSessionViewModel session in replaced)
                    {
                        RemoveController(session);
                    }
                }

                if (eventArgs.NewItems?.OfType<GameSessionViewModel>() is { } replacements)
                {
                    foreach (GameSessionViewModel session in replacements)
                    {
                        AddController(session);
                    }
                }

                break;
            case NotifyCollectionChangedAction.Reset:
                foreach (MacroSessionController controller in _controllers.Values.ToArray())
                {
                    RemoveController(controller.Session);
                }

                break;
        }
    }

    private void AddController(GameSessionViewModel session)
    {
        if (_disposed || _controllers.ContainsKey(session.Id))
        {
            return;
        }

        MacroSessionController? controller = null;
        try
        {
            controller = new MacroSessionController(session, _owner);
            _controllers.Add(session.Id, controller);
            _workspace.MacroAssistant.AddController(controller);
            controller = null;
        }
        catch (Exception exception)
        {
            if (controller is not null)
            {
                _controllers.Remove(session.Id);
                controller.Dispose();
            }

            DiagnosticLog.Current.WriteFailure(
                "macro.controller",
                "The macro controller could not be created for a game session.",
                exception,
                ("sessionId", session.Id.ToString()));
        }
    }

    private void RemoveController(GameSessionViewModel session)
    {
        if (!_controllers.Remove(session.Id, out MacroSessionController? controller))
        {
            return;
        }

        _workspace.MacroAssistant.RemoveController(controller);
    }
}

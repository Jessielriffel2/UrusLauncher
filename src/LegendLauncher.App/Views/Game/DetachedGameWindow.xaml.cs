using System.Windows;
using LegendLauncher.App.ViewModels;

namespace LegendLauncher.App.Views.Game;

public partial class DetachedGameWindow : Window
{
    private readonly DetachedWindowCloseState _closeState = new();

    internal DetachedGameWindow(
        GameSessionViewModel session,
        GameWorkspaceViewModel workspace)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        InitializeComponent();
        BorderlessWindowWorkArea.Attach(this);
        DataContext = new DetachedGameWindowContext(Session, Workspace);
        StateChanged += OnWindowStateChanged;
    }

    internal event EventHandler? ReattachRequested;

    internal GameSessionViewModel Session { get; }

    internal GameWorkspaceViewModel Workspace { get; }

    internal void CloseWithoutReattaching()
    {
        RequestClose(suppressReattach: true);
    }

    protected override void OnClosed(EventArgs eventArgs)
    {
        StateChanged -= OnWindowStateChanged;
        base.OnClosed(eventArgs);
        if (_closeState.TryRaiseReattach(Workspace.Sessions.Contains(Session)))
        {
            ReattachRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ReattachButton_OnClick(object sender, RoutedEventArgs eventArgs) =>
        RequestClose(suppressReattach: false);

    private void CloseSessionButton_OnClick(object sender, RoutedEventArgs eventArgs)
    {
        if (!Workspace.CloseSessionCommand.CanExecute(Session))
        {
            RequestClose(suppressReattach: false);
            return;
        }

        _closeState.SuppressReattach();
        try
        {
            Workspace.CloseSessionCommand.Execute(Session);
        }
        catch
        {
            _closeState.AllowReattach();
            throw;
        }

        bool wasRemoved = !Workspace.Sessions.Contains(Session);
        if (!wasRemoved)
        {
            _closeState.AllowReattach();
        }

        RequestClose(suppressReattach: wasRemoved);
    }

    private void MinimizeButton_OnClick(object sender, RoutedEventArgs eventArgs) =>
        BorderlessWindowCommands.Minimize(this);

    private void MaximizeButton_OnClick(object sender, RoutedEventArgs eventArgs) => ToggleMaximize();

    private void CloseButton_OnClick(object sender, RoutedEventArgs eventArgs) =>
        RequestClose(suppressReattach: false);

    private void RequestClose(bool suppressReattach)
    {
        if (!_closeState.TryBeginClose(suppressReattach))
        {
            return;
        }

        try
        {
            Close();
        }
        catch
        {
            _closeState.CancelCloseRequest();
            throw;
        }
    }

    private void ToggleMaximize() => BorderlessWindowCommands.ToggleMaximize(this);

    private void OnWindowStateChanged(object? sender, EventArgs eventArgs)
    {
        MaximizeButton.Content = BorderlessWindowCommands.GetMaximizeGlyph(WindowState);
    }
}

internal sealed class DetachedGameWindowContext(
    GameSessionViewModel session,
    GameWorkspaceViewModel workspace)
{
    public GameSessionViewModel Session { get; } = session;

    public GameWorkspaceViewModel Workspace { get; } = workspace;
}

internal sealed class DetachedWindowCloseState
{
    private bool _closeRequested;
    private bool _reattachRaised;
    private bool _suppressReattach;

    public void SuppressReattach() => _suppressReattach = true;

    public void AllowReattach() => _suppressReattach = false;

    public bool TryBeginClose(bool suppressReattach)
    {
        _suppressReattach |= suppressReattach;
        if (_closeRequested)
        {
            return false;
        }

        _closeRequested = true;
        return true;
    }

    public void CancelCloseRequest() => _closeRequested = false;

    public bool TryRaiseReattach(bool sessionExists)
    {
        if (_suppressReattach || _reattachRaised || !sessionExists)
        {
            return false;
        }

        _reattachRaised = true;
        return true;
    }
}

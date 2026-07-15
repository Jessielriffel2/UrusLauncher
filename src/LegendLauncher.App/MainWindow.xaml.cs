using System.ComponentModel;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using LegendLauncher.App.ViewModels;
using LegendLauncher.App.Views.Game;

namespace LegendLauncher.App;

public partial class MainWindow : Window
{
    private readonly HttpClient _httpClient;
    private readonly MainWindowViewModel _viewModel;
    private readonly Dictionary<Guid, DetachedGameWindow> _detachedWindows = [];
    private bool _initialized;
    private bool _isClosing;

    public MainWindow()
    {
        InitializeComponent();
        BorderlessWindowWorkArea.Attach(this);

        _httpClient = LauncherComposition.CreateHttpClient();
        _viewModel = LauncherComposition.CreateMainWindowViewModel(_httpClient);
        _viewModel.PropertyChanged += ViewModelOnPropertyChanged;
        _viewModel.UpdateInstallerStarted += ViewModelOnUpdateInstallerStarted;
        _viewModel.Workspace.DetachRequested += WorkspaceOnDetachRequested;
        _viewModel.Workspace.SessionRemoved += WorkspaceOnSessionRemoved;
        DataContext = _viewModel;

        Loaded += OnLoaded;
        Closing += OnClosing;
        Closed += OnClosed;
        StateChanged += OnWindowStateChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs eventArgs)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        await _viewModel.InitializeAsync();
    }

    private void PasswordInput_OnPasswordChanged(object sender, RoutedEventArgs eventArgs)
    {
        _viewModel.PendingPassword = PasswordInput.Password;
        PasswordPlaceholder.Visibility = PasswordInput.Password.Length == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void PlayButton_OnClick(object sender, RoutedEventArgs eventArgs)
    {
        _viewModel.PendingPassword = PasswordInput.Password;
    }

    private void ComboBoxSelector_OnPreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs eventArgs)
    {
        if (sender is not ComboBox selector || selector.IsDropDownOpen)
        {
            return;
        }

        selector.Focus();
        selector.IsDropDownOpen = true;
        eventArgs.Handled = true;
    }

    private void ComboBoxSelector_OnPreviewKeyDown(object sender, KeyEventArgs eventArgs)
    {
        if (sender is not ComboBox selector || selector.IsDropDownOpen)
        {
            return;
        }

        bool shouldOpen = eventArgs.Key is Key.Enter or Key.Space or Key.F4 ||
            eventArgs.Key == Key.Down && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);
        if (!shouldOpen)
        {
            return;
        }

        selector.IsDropDownOpen = true;
        eventArgs.Handled = true;
    }

    private void ProfileMenuButton_OnClick(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Button { DataContext: ProfileItemViewModel profile } button)
        {
            return;
        }

        _viewModel.SelectedProfile = profile;
        ContextMenu menu = BuildRecentServersMenu(button);
        button.ContextMenu = menu;
        menu.IsOpen = true;
        eventArgs.Handled = true;
    }

    private ContextMenu BuildRecentServersMenu(Button placementTarget)
    {
        var menu = new ContextMenu
        {
            Placement = PlacementMode.Bottom,
            PlacementTarget = placementTarget,
            Style = (Style)FindResource("RecentServersContextMenuStyle"),
        };
        Style itemStyle = (Style)FindResource("RecentServerMenuItemStyle");
        var playItem = new MenuItem
        {
            Header = _viewModel.Localization.Get("Menu_Play"),
            Style = itemStyle,
            IsEnabled = _viewModel.PlayCommand.CanExecute(null),
        };
        playItem.Click += PlayProfileMenuItem_OnClick;
        menu.Items.Add(playItem);

        var editItem = new MenuItem
        {
            Header = _viewModel.Localization.Get("Menu_EditProfile"),
            Style = itemStyle,
            IsEnabled = _viewModel.EditProfileCommand.CanExecute(null),
        };
        editItem.Click += EditProfileMenuItem_OnClick;
        menu.Items.Add(editItem);
        menu.Items.Add(new Separator());

        menu.Items.Add(new MenuItem
        {
            Header = _viewModel.Localization.Get("Menu_RecentServers"),
            FontSize = 9,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("AccentBrush"),
            Focusable = false,
            IsHitTestVisible = false,
            Style = itemStyle,
        });

        if (_viewModel.RecentServers.Count == 0)
        {
            menu.Items.Add(new MenuItem
            {
                Header = _viewModel.Localization.Get("Menu_NoRecentServers"),
                IsEnabled = false,
                Style = itemStyle,
            });
        }
        else
        {
            foreach (ServerRowViewModel server in _viewModel.RecentServers)
            {
                var item = new MenuItem
                {
                    Header = $"{server.Code}   {server.Name}",
                    IsEnabled = _viewModel.SelectRecentServerCommand.CanExecute(server),
                    Style = itemStyle,
                    Tag = server,
                };
                item.Click += RecentServerMenuItem_OnClick;
                menu.Items.Add(item);
            }
        }

        menu.Items.Add(new Separator());
        var deleteItem = new MenuItem
        {
            Header = _viewModel.Localization.Get("Menu_DeleteProfile"),
            Foreground = (Brush)FindResource("DangerBrush"),
            Style = itemStyle,
            IsEnabled = _viewModel.DeleteProfileCommand.CanExecute(null),
        };
        deleteItem.Click += DeleteProfileMenuItem_OnClick;
        menu.Items.Add(deleteItem);

        return menu;
    }

    private void RecentServerMenuItem_OnClick(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is MenuItem { Tag: ServerRowViewModel server } &&
            _viewModel.SelectRecentServerCommand.CanExecute(server))
        {
            _viewModel.SelectRecentServerCommand.Execute(server);
        }
    }

    private void PlayProfileMenuItem_OnClick(object sender, RoutedEventArgs eventArgs)
    {
        if (_viewModel.PlayCommand.CanExecute(null))
        {
            _viewModel.PlayCommand.Execute(null);
        }
    }

    private void EditProfileMenuItem_OnClick(object sender, RoutedEventArgs eventArgs)
    {
        if (_viewModel.EditProfileCommand.CanExecute(null))
        {
            _viewModel.EditProfileCommand.Execute(null);
        }
    }

    private void DeleteProfileMenuItem_OnClick(object sender, RoutedEventArgs eventArgs)
    {
        if (_viewModel.DeleteProfileCommand.CanExecute(null))
        {
            _viewModel.DeleteProfileCommand.Execute(null);
        }
    }

    private void MinimizeButton_OnClick(object sender, RoutedEventArgs eventArgs) =>
        BorderlessWindowCommands.Minimize(this);

    private void MaximizeButton_OnClick(object sender, RoutedEventArgs eventArgs) => ToggleMaximize();

    private void CloseButton_OnClick(object sender, RoutedEventArgs eventArgs) => Close();

    private void ToggleMaximize() => BorderlessWindowCommands.ToggleMaximize(this);

    private void OnWindowStateChanged(object? sender, EventArgs eventArgs)
    {
        MaximizeButton.Content = BorderlessWindowCommands.GetMaximizeGlyph(WindowState);
    }

    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if ((eventArgs.PropertyName is nameof(MainWindowViewModel.SelectedProfile) or nameof(MainWindowViewModel.PendingPassword)) &&
            string.IsNullOrEmpty(_viewModel.PendingPassword) &&
            PasswordInput.Password.Length > 0)
        {
            PasswordInput.Clear();
        }
    }

    private void OnClosing(object? sender, CancelEventArgs eventArgs)
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        DetachedWindowCoordinator.CloseAll(
            _detachedWindows,
            window =>
            {
                window.ReattachRequested -= DetachedWindowOnReattachRequested;
                window.CloseWithoutReattaching();
            });
    }

    private void OnClosed(object? sender, EventArgs eventArgs)
    {
        Closing -= OnClosing;
        StateChanged -= OnWindowStateChanged;
        _viewModel.PropertyChanged -= ViewModelOnPropertyChanged;
        _viewModel.UpdateInstallerStarted -= ViewModelOnUpdateInstallerStarted;
        _viewModel.Workspace.DetachRequested -= WorkspaceOnDetachRequested;
        _viewModel.Workspace.SessionRemoved -= WorkspaceOnSessionRemoved;
        _viewModel.Dispose();
        _httpClient.Dispose();
    }

    private void ViewModelOnUpdateInstallerStarted(object? sender, EventArgs eventArgs) =>
        Dispatcher.BeginInvoke(Close);

    private void WorkspaceOnDetachRequested(object? sender, GameSessionViewModel session)
    {
        if (_isClosing)
        {
            _viewModel.Workspace.Reattach(session);
            return;
        }

        if (_detachedWindows.TryGetValue(session.Id, out DetachedGameWindow? existing))
        {
            existing.Activate();
            return;
        }

        _ = DetachedWindowCoordinator.TryShow(
            session.Id,
            _detachedWindows,
            create: () => new DetachedGameWindow(session, _viewModel.Workspace),
            initialize: window =>
            {
                window.Owner = this;
                window.ReattachRequested += DetachedWindowOnReattachRequested;
            },
            show: window => window.Show(),
            cleanup: window =>
            {
                window.ReattachRequested -= DetachedWindowOnReattachRequested;
                window.CloseWithoutReattaching();
            },
            rollback: () => _viewModel.Workspace.Reattach(session));
    }

    private void WorkspaceOnSessionRemoved(object? sender, GameSessionViewModel session)
    {
        if (!_detachedWindows.Remove(session.Id, out DetachedGameWindow? window))
        {
            return;
        }

        window.ReattachRequested -= DetachedWindowOnReattachRequested;
        window.CloseWithoutReattaching();
    }

    private void DetachedWindowOnReattachRequested(object? sender, EventArgs eventArgs)
    {
        if (sender is not DetachedGameWindow window)
        {
            return;
        }

        window.ReattachRequested -= DetachedWindowOnReattachRequested;
        _detachedWindows.Remove(window.Session.Id);
        if (!_isClosing)
        {
            _viewModel.Workspace.Reattach(window.Session);
        }
    }
}

internal static class DetachedWindowCoordinator
{
    public static bool TryShow<TWindow>(
        Guid sessionId,
        Dictionary<Guid, TWindow> windows,
        Func<TWindow> create,
        Action<TWindow> initialize,
        Action<TWindow> show,
        Action<TWindow> cleanup,
        Action rollback)
        where TWindow : class
    {
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(create);
        ArgumentNullException.ThrowIfNull(initialize);
        ArgumentNullException.ThrowIfNull(show);
        ArgumentNullException.ThrowIfNull(cleanup);
        ArgumentNullException.ThrowIfNull(rollback);

        TWindow? window = null;
        bool registered = false;
        try
        {
            window = create();
            initialize(window);
            registered = windows.TryAdd(sessionId, window);
            if (!registered)
            {
                TryCleanup(window, cleanup);
                return false;
            }

            show(window);
            return true;
        }
        catch (Exception exception)
        {
            if (registered)
            {
                windows.Remove(sessionId);
            }

            if (window is not null)
            {
                TryCleanup(window, cleanup);
            }

            rollback();
            System.Diagnostics.Debug.WriteLine(
                $"Detached window creation failed and was rolled back: {exception}");
            return false;
        }
    }

    public static void CloseAll<TWindow>(
        Dictionary<Guid, TWindow> windows,
        Action<TWindow> close)
        where TWindow : class
    {
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(close);

        foreach (TWindow window in windows.Values.ToArray())
        {
            TryCleanup(window, close);
        }

        windows.Clear();
    }

    private static void TryCleanup<TWindow>(TWindow window, Action<TWindow> cleanup)
    {
        try
        {
            cleanup(window);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Detached window cleanup failed: {exception}");
        }
    }
}

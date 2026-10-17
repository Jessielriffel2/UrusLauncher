using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LegendLauncher.App.ViewModels;

namespace LegendLauncher.App.Views.Game;

public partial class GameWorkspaceView : UserControl
{
    public GameWorkspaceView()
    {
        InitializeComponent();
    }

    /// <summary>Double-click on the color control returns the game to its original colors.</summary>
    private void ColorSaturationControl_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (eventArgs.ClickCount == 2 &&
            DataContext is MainWindowViewModel { Workspace: { } workspace } &&
            workspace.ResetColorSaturationCommand.CanExecute(null))
        {
            workspace.ResetColorSaturationCommand.Execute(null);
            eventArgs.Handled = true;
        }
    }

    private void CompactGameToolbar_OnMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs eventArgs)
    {
        BorderlessWindowCommands.TryDrag(Window.GetWindow(this), eventArgs);
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
}

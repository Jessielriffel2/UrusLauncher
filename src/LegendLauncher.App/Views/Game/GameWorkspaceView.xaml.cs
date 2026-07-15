using System.Windows.Controls;
using System.Windows.Input;

namespace LegendLauncher.App.Views.Game;

public partial class GameWorkspaceView : UserControl
{
    public GameWorkspaceView()
    {
        InitializeComponent();
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

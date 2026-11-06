using System.Windows.Controls;
using System.Windows.Input;

namespace LegendLauncher.App.Views.Game;

/// <summary>
/// Explicit opening of the game toolbar selectors (language, zoom): inside the window chrome a plain click would start a window drag
/// instead of opening the list, so the first click or Enter/Space/F4/Alt+Down opens it.
/// </summary>
internal static class ToolbarComboBoxOpening
{
    public static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (sender is not ComboBox selector || selector.IsDropDownOpen)
        {
            return;
        }

        selector.Focus();
        selector.IsDropDownOpen = true;
        eventArgs.Handled = true;
    }

    public static void OnPreviewKeyDown(object sender, KeyEventArgs eventArgs)
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

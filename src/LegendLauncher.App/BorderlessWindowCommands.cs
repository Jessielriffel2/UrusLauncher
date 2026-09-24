using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace LegendLauncher.App;

internal static class BorderlessWindowCommands
{
    internal const string MaximizeGlyph = "\uE922";
    internal const string RestoreGlyph = "\uE923";

    public static void Minimize(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        SystemCommands.MinimizeWindow(window);
    }

    public static void ToggleMaximize(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (GetMaximizeAction(window.WindowState) == MaximizeAction.Restore)
        {
            SystemCommands.RestoreWindow(window);
            return;
        }

        SystemCommands.MaximizeWindow(window);
    }

    internal static void TryDrag(Window? window, MouseButtonEventArgs eventArgs)
    {
        if (window is null || eventArgs.ButtonState != MouseButtonState.Pressed ||
            IsInteractive(eventArgs.OriginalSource as DependencyObject))
        {
            return;
        }

        try
        {
            window.DragMove();
            eventArgs.Handled = true;
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static bool IsInteractive(DependencyObject? source)
    {
        DependencyObject? current = source;
        while (current is not null)
        {
            if (current is ButtonBase or TextBoxBase or ComboBox or ListBox or Slider or
                ScrollBar or ToggleButton or PasswordBox)
            {
                return true;
            }

            current = current is Visual or Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return false;
    }

    internal static MaximizeAction GetMaximizeAction(WindowState windowState) =>
        windowState == WindowState.Maximized
            ? MaximizeAction.Restore
            : MaximizeAction.Maximize;

    internal static string GetMaximizeGlyph(WindowState windowState) =>
        windowState == WindowState.Maximized ? RestoreGlyph : MaximizeGlyph;

    internal enum MaximizeAction
    {
        Maximize,
        Restore,
    }
}

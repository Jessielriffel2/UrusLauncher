using System.Windows;

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

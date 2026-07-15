using System.Windows;
using LegendLauncher.App;

namespace LegendLauncher.Tests.App;

public sealed class BorderlessWindowCommandsTests
{
    [Theory]
    [InlineData(WindowState.Normal, false)]
    [InlineData(WindowState.Minimized, false)]
    [InlineData(WindowState.Maximized, true)]
    public void MaximizeActionMatchesCurrentWindowState(
        WindowState state,
        bool shouldRestore)
    {
        BorderlessWindowCommands.MaximizeAction expected = shouldRestore
            ? BorderlessWindowCommands.MaximizeAction.Restore
            : BorderlessWindowCommands.MaximizeAction.Maximize;
        Assert.Equal(expected, BorderlessWindowCommands.GetMaximizeAction(state));
    }

    [Theory]
    [InlineData(WindowState.Normal, BorderlessWindowCommands.MaximizeGlyph)]
    [InlineData(WindowState.Minimized, BorderlessWindowCommands.MaximizeGlyph)]
    [InlineData(WindowState.Maximized, BorderlessWindowCommands.RestoreGlyph)]
    public void CaptionGlyphMatchesCurrentWindowState(WindowState state, string expected)
    {
        Assert.Equal(expected, BorderlessWindowCommands.GetMaximizeGlyph(state));
    }
}

using LegendLauncher.App.ViewModels;

namespace LegendLauncher.Tests.App;

public sealed class PasswordSelectionTests
{
    [Theory]
    [InlineData("daCarta", "", "daCarta")]
    [InlineData("daCarta", "doEditor", "daCarta")]
    [InlineData("", "doEditor", "doEditor")]
    [InlineData(null, "doEditor", "doEditor")]
    public void FromBoxes_PrefersWhateverWasTyped(string? card, string? editor, string expected)
    {
        Assert.Equal(expected, PasswordSelection.FromBoxes(card, editor));
    }

    [Theory]
    [InlineData("", "")]
    [InlineData(null, null)]
    [InlineData("", null)]
    public void FromBoxes_NeverReturnsAnEmptyStringSoItCannotEraseAnExistingPassword(string? card, string? editor)
    {
        Assert.Null(PasswordSelection.FromBoxes(card, editor));
    }
}

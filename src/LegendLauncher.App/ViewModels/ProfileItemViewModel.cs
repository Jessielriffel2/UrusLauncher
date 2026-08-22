using LegendLauncher.Core.Models;

namespace LegendLauncher.App.ViewModels;

internal sealed class ProfileItemViewModel(AccountProfile model)
{
    public AccountProfile Model { get; } = model;

    public string DisplayName => Model.DisplayName;

    public string Initial => string.IsNullOrWhiteSpace(DisplayName)
        ? "?"
        : DisplayName.Trim()[..1].ToUpperInvariant();

    public string Summary => $"{Model.UserName} · {Model.PlatformId}";

    public bool MatchesSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        string needle = query.Trim();
        return DisplayName.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
            Model.UserName.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }
}

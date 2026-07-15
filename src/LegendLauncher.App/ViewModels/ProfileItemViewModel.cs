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
}

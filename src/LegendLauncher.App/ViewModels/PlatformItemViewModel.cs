using LegendLauncher.Core.Models;

namespace LegendLauncher.App.ViewModels;

internal sealed class PlatformItemViewModel(PlatformDefinition model)
{
    public PlatformDefinition Model { get; } = model;

    public string Id => Model.Id;

    public string DisplayName => Model.DisplayName;
}

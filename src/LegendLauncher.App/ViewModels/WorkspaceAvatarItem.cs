namespace LegendLauncher.App.ViewModels;

internal sealed class WorkspaceAvatarItem(
    GameSessionViewModel front,
    GameSessionViewModel? rear,
    string toolTip,
    string automationName)
{
    public GameSessionViewModel Front { get; } = front;

    public GameSessionViewModel? Rear { get; } = rear;

    public bool IsPair => Rear is not null;

    public string FrontInitial => Front.Initial;

    public string RearInitial => Rear?.Initial ?? string.Empty;

    public bool IsHighlighted =>
        Front.IsSelected || Rear is { IsSelected: true };

    public string ToolTip { get; } = toolTip;

    public string AutomationName { get; } = automationName;
}

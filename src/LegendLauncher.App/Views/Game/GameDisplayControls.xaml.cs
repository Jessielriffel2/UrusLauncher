using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LegendLauncher.App.ViewModels;

namespace LegendLauncher.App.Views.Game;

/// <summary>Color saturation and game zoom controls of the game toolbar.</summary>
public partial class GameDisplayControls : UserControl
{
    /// <summary>Last level of <see cref="ApplyCompactLevel"/>: 0 = everything shown (level 1 is the toolbar's language selector).</summary>
    internal const int MostCompactLevel = 4;

    private const double CompactColorSliderWidth = 80;
    private const double FullColorSliderWidth = 110;
    private const double CompactZoomWidth = 130;
    private const double FullZoomWidth = 172;

    public GameDisplayControls()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Narrow toolbar (see GameWorkspaceView.FitToolbarToWidth): from level 2 the "Colors" label hides and its slider shrinks; from
    /// level 3 the zoom selector gets narrower; at level 4 the DirectX switch keeps only its icon (its tooltip still explains it).
    /// </summary>
    internal void ApplyCompactLevel(int level)
    {
        ColorSaturationLabel.Visibility = level >= 2 ? Visibility.Collapsed : Visibility.Visible;
        ColorSaturationSlider.Width = level >= 2 ? CompactColorSliderWidth : FullColorSliderWidth;
        WorkspaceZoomSelector.Width = level >= 3 ? CompactZoomWidth : FullZoomWidth;
        WindowModeLabel.Visibility = level >= 4 ? Visibility.Collapsed : Visibility.Visible;
        WindowModeIcon.Margin = level >= 4 ? new Thickness(0) : new Thickness(0, 0, 7, 0);
    }

    /// <summary>Double-click on the color control returns the game to its original colors.</summary>
    private void ColorSaturationControl_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (eventArgs.ClickCount == 2 &&
            DataContext is MainWindowViewModel { Workspace: { } workspace } &&
            workspace.ResetColorSaturationCommand.CanExecute(null))
        {
            workspace.ResetColorSaturationCommand.Execute(null);
            eventArgs.Handled = true;
        }
    }

    private void ComboBoxSelector_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs) =>
        ToolbarComboBoxOpening.OnPreviewMouseLeftButtonDown(sender, eventArgs);

    private void ComboBoxSelector_OnPreviewKeyDown(object sender, KeyEventArgs eventArgs) =>
        ToolbarComboBoxOpening.OnPreviewKeyDown(sender, eventArgs);
}

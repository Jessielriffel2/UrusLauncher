using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LegendLauncher.App.ViewModels;

namespace LegendLauncher.App.Views.Game;

public partial class GameWorkspaceView : UserControl
{
    public GameWorkspaceView()
    {
        InitializeComponent();
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

    // Fixed parts of the toolbar besides the Auto columns: the three 8-unit gaps and the space under the window buttons.
    private const double ToolbarGapsWidth = 3 * 8;
    private const double CompactColorSliderWidth = 80;
    private const double FullColorSliderWidth = 110;

    private bool _fittingToolbar;

    private void CompactGameToolbar_OnSizeChanged(object sender, SizeChangedEventArgs eventArgs) => FitToolbarToWidth();

    /// <summary>
    /// On a narrow toolbar (a small window, or a high Windows scale: a 4K TV at 300% is only 1280 units wide) the optional parts
    /// give way so the action buttons never slide under the window buttons (a click meant for "Detach" closed the launcher):
    /// first the language selector (it is also on the launcher screen), then the "Colors" label and part of its slider.
    /// They come back as soon as there is room again.
    /// </summary>
    private void FitToolbarToWidth()
    {
        if (_fittingToolbar || CompactGameToolbar.ActualWidth <= 0)
        {
            return;
        }

        _fittingToolbar = true;
        try
        {
            double available = CompactGameToolbar.ActualWidth -
                CompactGameToolbar.ColumnDefinitions[0].ActualWidth -
                CompactGameToolbar.ColumnDefinitions[4].ActualWidth -
                ToolbarGapsWidth -
                MainWindowCaptionButtonReservedSpace.Width;
            for (int level = 0; level <= 2; level++)
            {
                ApplyToolbarLevel(level);
                WorkspaceActionGroup.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                if (WorkspaceActionGroup.DesiredSize.Width <= available)
                {
                    break;
                }
            }
        }
        finally
        {
            _fittingToolbar = false;
        }
    }

    private void ApplyToolbarLevel(int level)
    {
        WorkspaceLanguageSelector.Visibility = level >= 1 ? Visibility.Collapsed : Visibility.Visible;
        ColorSaturationLabel.Visibility = level >= 2 ? Visibility.Collapsed : Visibility.Visible;
        ColorSaturationSlider.Width = level >= 2 ? CompactColorSliderWidth : FullColorSliderWidth;
    }

    private void CompactGameToolbar_OnMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs eventArgs)
    {
        BorderlessWindowCommands.TryDrag(Window.GetWindow(this), eventArgs);
    }

    private void ComboBoxSelector_OnPreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs eventArgs)
    {
        if (sender is not ComboBox selector || selector.IsDropDownOpen)
        {
            return;
        }

        selector.Focus();
        selector.IsDropDownOpen = true;
        eventArgs.Handled = true;
    }

    private void ComboBoxSelector_OnPreviewKeyDown(object sender, KeyEventArgs eventArgs)
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

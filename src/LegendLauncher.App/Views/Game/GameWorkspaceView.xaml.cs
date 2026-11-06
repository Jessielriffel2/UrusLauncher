using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LegendLauncher.App.ViewModels;

namespace LegendLauncher.App.Views.Game;

public partial class GameWorkspaceView : UserControl
{
    public GameWorkspaceView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    // Toolbar texts that change on their own (mode switched, sound, language): the toolbar may now need more or less room, so
    // the fit starts again from the full toolbar. These come from the view model, never from the fit itself.
    private static readonly HashSet<string> s_toolbarTextProperties =
    [
        nameof(GameWorkspaceViewModel.WindowModeLabel),
        nameof(GameWorkspaceViewModel.MuteLabel),
    ];

    private GameWorkspaceViewModel? _workspace;

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs eventArgs)
    {
        if (_workspace is not null)
        {
            _workspace.PropertyChanged -= WorkspaceOnPropertyChanged;
        }

        _workspace = (eventArgs.NewValue as MainWindowViewModel)?.Workspace;
        if (_workspace is not null)
        {
            _workspace.PropertyChanged += WorkspaceOnPropertyChanged;
        }
    }

    private void WorkspaceOnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is { } name && s_toolbarTextProperties.Contains(name))
        {
            _lastGivenToolbarWidth = 0;
            _ = Dispatcher.InvokeAsync(FitToolbarToWidth, DispatcherPriority.Loaded);
        }
    }

    // Fixed parts of the toolbar besides the Auto columns: the three 8-unit gaps and the space under the window buttons.
    private const double ToolbarGapsWidth = 3 * 8;

    private bool _fittingToolbar;
    private int _toolbarLevel;
    private bool _toolbarLevelApplied;
    private double _lastGivenToolbarWidth;

    private void CompactGameToolbar_OnSizeChanged(object sender, SizeChangedEventArgs eventArgs) => FitToolbarToWidth();

    /// <summary>
    /// On a narrow toolbar (a small window, or a high Windows scale: a 4K TV at 300% is only 1280 units wide) the optional parts
    /// give way so the action buttons never slide under the window buttons (a click meant for "Detach" closed the launcher):
    /// first the language selector (it is also on the launcher screen), then the "Colors" label and part of its slider, then the
    /// DirectX label (icon only) and part of the zoom selector.
    /// </summary>
    /// <remarks>
    /// Every level change resizes the toolbar and fires this handler again, so a pass must not go back to the full toolbar just
    /// to measure it: that toggled the parts on every call and kept the layout running forever. The toolbar only gets more compact
    /// while it does not fit, and only tries the fuller levels again when the window gives it more width.
    /// </remarks>
    private void FitToolbarToWidth()
    {
        double toolbarWidth = GivenToolbarWidth();
        if (_fittingToolbar || toolbarWidth <= 0)
        {
            return;
        }

        _fittingToolbar = true;
        try
        {
            double available = toolbarWidth -
                CompactGameToolbar.ColumnDefinitions[0].ActualWidth -
                CompactGameToolbar.ColumnDefinitions[4].ActualWidth -
                ToolbarGapsWidth -
                MainWindowCaptionButtonReservedSpace.Width;
            bool gotWider = toolbarWidth > _lastGivenToolbarWidth + 0.5;
            _lastGivenToolbarWidth = toolbarWidth;
            int level = gotWider ? 0 : _toolbarLevel;
            while (true)
            {
                if (SetToolbarLevel(level))
                {
                    // The parts that change sit inside other controls (DirectX label, slider, zoom), whose new size only
                    // reaches the action group in the next layout pass; without this the measure below is stale.
                    InvalidateMeasureDeep(WorkspaceActionGroup);
                }

                WorkspaceActionGroup.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                if (WorkspaceActionGroup.DesiredSize.Width <= available || level >= GameDisplayControls.MostCompactLevel)
                {
                    break;
                }

                level++;
            }
        }
        finally
        {
            _fittingToolbar = false;
        }
    }

    /// <summary>Applies a compaction level; false when it was already the applied one (nothing changed).</summary>
    private bool SetToolbarLevel(int level)
    {
        if (level == _toolbarLevel && _toolbarLevelApplied)
        {
            return false;
        }

        _toolbarLevel = level;
        _toolbarLevelApplied = true;
        ApplyToolbarLevel(level);
        return true;
    }

    private static void InvalidateMeasureDeep(DependencyObject element)
    {
        if (element is UIElement uiElement)
        {
            uiElement.InvalidateMeasure();
        }

        int count = VisualTreeHelper.GetChildrenCount(element);
        for (int index = 0; index < count; index++)
        {
            InvalidateMeasureDeep(VisualTreeHelper.GetChild(element, index));
        }
    }

    private void ApplyToolbarLevel(int level)
    {
        WorkspaceLanguageSelector.Visibility = level >= 1 ? Visibility.Collapsed : Visibility.Visible;
        WorkspaceDisplayControls.ApplyCompactLevel(level);
    }

    /// <summary>
    /// The width the window gives the toolbar. Not its ActualWidth: when the toolbar overflows, WPF arranges it at the full
    /// width of its content (and clips it), so ActualWidth grows with the overflow and the toolbar always seemed to fit (on a
    /// 1420-wide window "Detach" ended up under the window buttons). The layout slot is the space the parent really assigned.
    /// </summary>
    private double GivenToolbarWidth()
    {
        double slot = LayoutInformation.GetLayoutSlot(CompactGameToolbar).Width;
        return slot > 0 ? Math.Min(slot, CompactGameToolbar.ActualWidth) : CompactGameToolbar.ActualWidth;
    }

    private void CompactGameToolbar_OnMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs eventArgs)
    {
        BorderlessWindowCommands.TryDrag(Window.GetWindow(this), eventArgs);
    }

    private void ComboBoxSelector_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs) =>
        ToolbarComboBoxOpening.OnPreviewMouseLeftButtonDown(sender, eventArgs);

    private void ComboBoxSelector_OnPreviewKeyDown(object sender, KeyEventArgs eventArgs) =>
        ToolbarComboBoxOpening.OnPreviewKeyDown(sender, eventArgs);
}

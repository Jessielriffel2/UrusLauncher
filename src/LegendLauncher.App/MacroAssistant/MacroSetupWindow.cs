using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using LegendLauncher.App.GameHosting;
using LegendLauncher.App.Localization;

namespace LegendLauncher.App.MacroAssistant;

internal sealed class MacroSetupWindow : Window
{
    private readonly Canvas _canvas;
    private readonly Border _frame;
    private readonly Border _panel;
    private readonly Thumb _frameMoveThumb;
    private readonly Thumb _targetThumb;
    private readonly Thumb _resizeThumb;
    private readonly TextBlock _targetMarker;
    private readonly TextBlock _hint;
    private readonly TextBlock _status;
    private readonly StackPanel _clickSettingsPanel;
    private readonly TextBox _clickCountInput;
    private readonly TextBox _clickIntervalInput;
    private readonly TextBox _speedInput;
    private readonly ToggleButton _largeFrameToggle;
    private readonly StackPanel _largeFrameToggleRow;
    private readonly Button _playButton;
    private readonly Button _stopButton;
    private MacroMode _mode = MacroMode.Gems;
    private SurfaceGeometry _surface;
    private SurfaceRegion _region;
    private MacroPoint _target;
    private bool _useLargeFrame;
    private double _speed = 1.0;
    private bool _updating;

    public MacroSetupWindow(string sessionTitle, Window owner)
    {
        Title = $"Macro Assistant — {sessionTitle}";
        Owner = owner;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        // Not Topmost: owned by the window that shows the game, so it hides and minimizes with it.
        ShowActivated = true;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Focusable = true;

        var root = new Grid { Background = Brushes.Transparent };
        _canvas = new Canvas();
        root.Children.Add(_canvas);

        _frame = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(35, 230, 255)),
            BorderThickness = new Thickness(2),
            Background = new SolidColorBrush(Color.FromArgb(22, 35, 230, 255)),
        };
        _canvas.Children.Add(_frame);

        _frameMoveThumb = new Thumb
        {
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Focusable = false,
            Cursor = Cursors.SizeAll,
        };
        _frameMoveThumb.DragDelta += FrameDragDelta;
        _canvas.Children.Add(_frameMoveThumb);

        _resizeThumb = new Thumb
        {
            Width = 16,
            Height = 16,
            Background = new SolidColorBrush(Color.FromRgb(35, 230, 255)),
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(1),
            Focusable = false,
            Cursor = Cursors.SizeNWSE,
        };
        _resizeThumb.DragDelta += ResizeDragDelta;
        _canvas.Children.Add(_resizeThumb);

        _targetThumb = new Thumb
        {
            Width = 34,
            Height = 34,
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            Focusable = false,
            Cursor = Cursors.SizeAll,
        };
        _targetThumb.DragDelta += TargetDragDelta;
        _canvas.Children.Add(_targetThumb);

        _targetMarker = new TextBlock
        {
            Text = "+",
            Foreground = new SolidColorBrush(Color.FromRgb(53, 229, 216)),
            FontSize = 34,
            FontWeight = FontWeights.Bold,
            IsHitTestVisible = false,
            Opacity = 0.98,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                ShadowDepth = 1,
                BlurRadius = 3,
                Opacity = 0.85,
            },
        };
        // The aim must stay visible even when it is dragged under the settings panel.
        Panel.SetZIndex(_targetMarker, 40);
        Panel.SetZIndex(_targetThumb, 39);
        _canvas.Children.Add(_targetMarker);

        // The panel floats over the surface and can be dragged anywhere, so the user can
        // move it out of the way to see the whole game area.
        _panel = new Border
        {
            Width = 340,
            Padding = new Thickness(20, 16, 20, 18),
            Background = new SolidColorBrush(Color.FromArgb(246, 4, 13, 24)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(150, 35, 230, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(12),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Color.FromRgb(35, 230, 255),
                BlurRadius = 24,
                ShadowDepth = 0,
                Opacity = 0.22,
            },
        };
        var panelContent = new StackPanel();

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new TextBlock
        {
            Text = Localize("Macro_SetupTitle", "Macro Assistant"),
            Foreground = Brushes.White,
            FontSize = 17,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        });
        var grip = new TextBlock
        {
            Text = "⠿",
            Foreground = new SolidColorBrush(Color.FromRgb(98, 140, 165)),
            FontSize = 16,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            ToolTip = Localize("Macro_DragPanel", "Arraste para mover o painel"),
        };
        Grid.SetColumn(grip, 1);
        header.Children.Add(grip);
        panelContent.Children.Add(header);

        _hint = new TextBlock
        {
            Text = Localize("Macro_SetupHintVision", "Arraste a moldura para cobrir a área de análise."),
            Foreground = new SolidColorBrush(Color.FromRgb(173, 194, 211)),
            FontSize = 12,
            LineHeight = 17,
            Margin = new Thickness(0, 6, 0, 12),
            TextWrapping = TextWrapping.Wrap,
        };
        panelContent.Children.Add(_hint);
        _status = new TextBlock
        {
            Text = Localize("Macro_SetupStatus", "Ajuste a bolinha e pressione Play."),
            Foreground = new SolidColorBrush(Color.FromRgb(255, 223, 107)),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 14),
        };
        AutomationProperties.SetName(_status, "Macro setup status");
        panelContent.Children.Add(_status);
        panelContent.Children.Add(new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.FromArgb(70, 98, 140, 165)),
            Margin = new Thickness(0, 0, 0, 14),
        });

        _clickSettingsPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Visibility = Visibility.Collapsed,
        };
        _clickCountInput = CreateNumericInput("0", 1, 0, 9999, out Grid countHost);
        _clickSettingsPanel.Children.Add(CreateFieldRow(
            Localize("Macro_CountLabel", "Quantidade (0 = infinito)"),
            countHost));
        _clickIntervalInput = CreateNumericInput("0.10", 0.10, 0.01, 3600, out Grid intervalHost);
        _clickSettingsPanel.Children.Add(CreateFieldRow(
            Localize("Macro_IntervalLabel", "Intervalo entre cliques (s)"),
            intervalHost));
        panelContent.Children.Add(_clickSettingsPanel);

        _speedInput = CreateNumericInput("1.0", 0.10, 0.25, 4.0, out Grid speedHost);
        _speedInput.TextChanged += SpeedInputOnTextChanged;
        panelContent.Children.Add(CreateFieldRow(
            Localize("Macro_SpeedLabel", "Velocidade (0,25x–4x)"),
            speedHost));

        _largeFrameToggle = new ToggleButton
        {
            Width = 44,
            Height = 24,
            Cursor = Cursors.Hand,
            Focusable = true,
            Template = CreateLargeFrameToggleTemplate(),
        };
        _largeFrameToggle.Checked += LargeFrameToggleOnChanged;
        _largeFrameToggle.Unchecked += LargeFrameToggleOnChanged;
        _largeFrameToggleRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 2, 0, 14),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _largeFrameToggleRow.Children.Add(_largeFrameToggle);
        _largeFrameToggleRow.Children.Add(new TextBlock
        {
            Text = Localize("Macro_LargeFrame", "Usar moldura grande"),
            Foreground = Brushes.White,
            FontSize = 12.5,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
        });
        panelContent.Children.Add(_largeFrameToggleRow);

        var buttons = new Grid { Margin = new Thickness(0, 4, 0, 0) };
        buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _playButton = CreateButton("Play", Color.FromRgb(34, 138, 90));
        _playButton.Content = Localize("Macro_Play", "Play");
        _stopButton = CreateButton(
            Localize("Macro_Stop", "Parar"),
            Color.FromRgb(150, 56, 70));
        _playButton.Click += PlayButtonOnClick;
        _stopButton.Click += StopButtonOnClick;
        Grid.SetColumn(_stopButton, 2);
        buttons.Children.Add(_playButton);
        buttons.Children.Add(_stopButton);
        panelContent.Children.Add(buttons);

        // Title area used to drag the whole settings panel anywhere over the surface.
        var panelDragThumb = new Thumb
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.SizeAll,
            Focusable = false,
            Height = 40,
            VerticalAlignment = VerticalAlignment.Top,
        };
        panelDragThumb.DragDelta += PanelDragDelta;
        _panel.Child = new Grid
        {
            Children =
            {
                panelContent,
                panelDragThumb,
            },
        };
        // Placed on the canvas so it can be freely moved with Canvas coordinates.
        Canvas.SetLeft(_panel, 12);
        Canvas.SetTop(_panel, 12);
        _canvas.Children.Add(_panel);
        Content = root;
    }

    private void PanelDragDelta(object sender, DragDeltaEventArgs eventArgs)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        double currentLeft = double.IsNaN(Canvas.GetLeft(_panel)) ? 12 : Canvas.GetLeft(_panel);
        double currentTop = double.IsNaN(Canvas.GetTop(_panel)) ? 12 : Canvas.GetTop(_panel);
        double maxLeft = Math.Max(0, ActualWidth - _panel.ActualWidth);
        double maxTop = Math.Max(0, ActualHeight - _panel.ActualHeight);
        Canvas.SetLeft(
            _panel,
            Math.Clamp(currentLeft + eventArgs.HorizontalChange, 0, maxLeft));
        Canvas.SetTop(
            _panel,
            Math.Clamp(currentTop + eventArgs.VerticalChange, 0, maxTop));
    }

    public SurfaceRegion Region => _region;

    public MacroPoint Target => _target;

    public bool UseLargeFrame => _useLargeFrame;

    public double Speed => Math.Clamp(_speed, 0.25, 4.0);

    public event EventHandler? PlayRequested;

    public event EventHandler? StopRequested;

    public event EventHandler<SurfaceRegion>? RegionChanged;

    public event EventHandler? SettingsChanged;

    public void SetMode(MacroMode mode)
    {
        _mode = mode;
        bool isClicks = mode == MacroMode.Clicks;
        _clickSettingsPanel.Visibility = isClicks ? Visibility.Visible : Visibility.Collapsed;

        // Cosmo and Gems always run with the analysis frame and without the aim marker.
        _largeFrameToggleRow.Visibility = isClicks ? Visibility.Visible : Visibility.Collapsed;
        _updating = true;
        try
        {
            if (!isClicks)
            {
                _useLargeFrame = true;
            }

            _largeFrameToggle.IsChecked = _useLargeFrame;
        }
        finally
        {
            _updating = false;
        }

        _hint.Text = isClicks
            ? Localize(
                "Macro_SetupHint",
                "Arraste a moldura para posicionar a mira centralizada. Desligue o toggle para usar apenas a mira compacta.")
            : Localize("Macro_SetupHintVision", "Arraste a moldura para cobrir a área de análise. Neste modo a mira fica oculta.");
        SetStatus(isClicks
            ? Localize("Macro_SetupStatusClicks", "Posicione a moldura e configure os cliques.")
            : Localize("Macro_SetupStatusVision", "Ajuste a moldura sobre a área e pressione Play."));
        ApplyRegionToCanvas();
    }

    public ClickMacroSettings GetClickSettings()
    {
        int count = TryParseNumber(_clickCountInput.Text, out double parsedCount)
            ? Math.Max(0, (int)Math.Round(parsedCount))
            : 0;
        double seconds = TryParseNumber(_clickIntervalInput.Text, out double parsedSeconds)
            ? Math.Clamp(parsedSeconds, 0.01, 3600)
            : 0.10;
        return new ClickMacroSettings(count, TimeSpan.FromSeconds(seconds));
    }

    /// <summary>
    /// Accepts both "0.7" and "0,7" so the value is always accepted regardless of the
    /// keyboard layout, while the fields keep the dot as the single standard separator.
    /// </summary>
    private static bool TryParseNumber(string? text, out double value)
    {
        string normalized = (text ?? string.Empty).Trim().Replace(',', '.');
        return double.TryParse(
            normalized,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value);
    }

    public void ShowSetup(SurfaceGeometry surface, SurfaceRegion region)
    {
        MacroPoint target = new(
            region.HasArea ? region.X + region.Width / 2.0 : surface.Width / 2.0,
            region.HasArea ? region.Y + region.Height / 2.0 : surface.Height / 2.0);
        ShowSetup(surface, region, target, false, 1.0, new ClickMacroSettings(0, TimeSpan.FromSeconds(0.10)));
    }

    public void ShowSetup(
        SurfaceGeometry surface,
        SurfaceRegion region,
        MacroPoint target,
        bool useLargeFrame,
        double speed,
        ClickMacroSettings clickSettings)
    {
        _surface = surface;
        _region = ClampRegion(region.HasArea ? region : DefaultRegion(surface));
        _target = ClampPointToSurface(target, _surface);
        _useLargeFrame = useLargeFrame;
        _speed = Math.Clamp(speed, 0.25, 4.0);
        _updating = true;
        try
        {
            _largeFrameToggle.IsChecked = _useLargeFrame;
            _speedInput.Text = _speed.ToString("0.##", CultureInfo.InvariantCulture);
            _clickCountInput.Text = clickSettings.Count.ToString(CultureInfo.InvariantCulture);
            _clickIntervalInput.Text = clickSettings.Interval.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
        }
        finally
        {
            _updating = false;
        }

        SetMode(_mode);
        if (_useLargeFrame)
        {
            // The aim always sits at the center of the frame.
            _target = RegionCenter(_region);
        }
        else
        {
            // Compact aim: the analysis area is centered on the placed point, so the macro
            // cannot keep using a region saved from another position.
            _region = CenterRegionOnTarget(_region.Width, _region.Height);
        }

        if (!IsVisible)
        {
            Show();
            Activate();
            UpdateLayout();
        }

        NativeWindowMethods.SetOverlayBounds(
            new WindowInteropHelper(this).Handle,
            surface.X,
            surface.Y,
            surface.Width,
            surface.Height);
        ApplyRegionToCanvas();
    }

    public void ApplySurface(SurfaceGeometry surface)
    {
        SurfaceGeometry previous = _surface;
        if (previous.HasArea && surface.HasArea &&
            (previous.Width != surface.Width || previous.Height != surface.Height))
        {
            double scaleX = surface.Width / (double)previous.Width;
            double scaleY = surface.Height / (double)previous.Height;
            _target = new MacroPoint(_target.X * scaleX, _target.Y * scaleY);
            _region = new SurfaceRegion(
                (int)Math.Round(_region.X * scaleX),
                (int)Math.Round(_region.Y * scaleY),
                (int)Math.Round(_region.Width * scaleX),
                (int)Math.Round(_region.Height * scaleY));
        }

        _surface = surface;
        _region = ClampRegion(_region.HasArea ? _region : DefaultRegion(surface));
        _target = _useLargeFrame
            ? RegionCenter(_region)
            : ClampPointToSurface(_target, _surface);
        if (!_useLargeFrame)
        {
            // Compact aim: the analysis area always follows the point the user placed,
            // otherwise the macro would keep aiming at a stale region.
            _region = CenterRegionOnTarget(_region.Width, _region.Height);
        }

        if (IsVisible && surface.HasArea)
        {
            NativeWindowMethods.SetOverlayBounds(
                new WindowInteropHelper(this).Handle,
                surface.X,
                surface.Y,
                surface.Width,
                surface.Height);
            ApplyRegionToCanvas();
        }
    }

    public MacroProfilePreferences GetProfilePreferences(SurfaceGeometry surface)
    {
        double width = Math.Max(1, _region.Width);
        double height = Math.Max(1, _region.Height);
        return new MacroProfilePreferences(
            _target.X / Math.Max(1, surface.Width),
            _target.Y / Math.Max(1, surface.Height),
            _region.X / Math.Max(1, surface.Width),
            _region.Y / Math.Max(1, surface.Height),
            width / Math.Max(1, surface.Width),
            height / Math.Max(1, surface.Height),
            _useLargeFrame,
            Speed,
            GetClickSettings().Count,
            GetClickSettings().Interval.TotalSeconds).Normalized();
    }

    public void SetStatus(string status) => _status.Text = status;

    public void HideSetup() => Hide();

    /// <summary>Window that shows the game surface; this panel is owned by it.</summary>
    public void FollowHost(nint hostWindow)
    {
        if (new WindowInteropHelper(this).EnsureHandle() is { } handle && handle != nint.Zero)
        {
            NativeWindowMethods.SetWindowOwner(handle, hostWindow);
        }
    }

    private static string Localize(string key, string fallback) =>
        LocalizationService.Current.Get(key) is string value && value != $"[{key}]"
            ? value
            : fallback;

    private static TextBox CreateInput(string text) =>
        new()
        {
            Text = text,
            Width = 82,
            Height = 25,
            Padding = new Thickness(5, 2, 5, 2),
            Background = new SolidColorBrush(Color.FromRgb(8, 24, 40)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(71, 119, 142)),
            BorderThickness = new Thickness(1),
            VerticalContentAlignment = VerticalAlignment.Center,
        };

    /// <summary>One settings row: the label takes the free width and the field has a fixed one.</summary>
    private static Grid CreateFieldRow(string label, UIElement field)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = Brushes.White,
            FontSize = 12.5,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 14, 0),
        });
        Grid.SetColumn(field, 1);
        row.Children.Add(field);
        return row;
    }

    /// <summary>
    /// Builds a numeric field with up/down arrows inside one rounded frame. The arrows step the value
    /// by a fixed amount, keeping the dot as the separator.
    /// </summary>
    private static TextBox CreateNumericInput(
        string text,
        double step,
        double minimum,
        double maximum,
        out Grid host)
    {
        var input = new TextBox
        {
            Text = text,
            Height = 34,
            Padding = new Thickness(10, 0, 4, 0),
            Background = Brushes.Transparent,
            Foreground = Brushes.White,
            CaretBrush = Brushes.White,
            BorderThickness = new Thickness(0),
            FontSize = 13,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(input, text);

        var arrows = new Grid { Width = 28, Margin = new Thickness(0) };
        arrows.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        arrows.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Button up = CreateStepButton("▲", step, minimum, maximum, input, increase: true);
        Button down = CreateStepButton("▼", step, minimum, maximum, input, increase: false);
        Grid.SetRow(down, 1);
        arrows.Children.Add(up);
        arrows.Children.Add(down);

        var inner = new Grid();
        inner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        inner.Children.Add(input);
        Grid.SetColumn(arrows, 1);
        inner.Children.Add(arrows);

        var frame = new Border
        {
            CornerRadius = new CornerRadius(9),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(71, 119, 142)),
            Background = new SolidColorBrush(Color.FromRgb(8, 24, 40)),
            ClipToBounds = true,
            Child = inner,
        };
        input.GotKeyboardFocus += (_, _) =>
            frame.BorderBrush = new SolidColorBrush(Color.FromRgb(35, 230, 255));
        input.LostKeyboardFocus += (_, _) =>
            frame.BorderBrush = new SolidColorBrush(Color.FromRgb(71, 119, 142));

        host = new Grid { Width = 124, Height = 34 };
        host.Children.Add(frame);
        return input;
    }

    private static Button CreateStepButton(
        string glyph,
        double step,
        double minimum,
        double maximum,
        TextBox input,
        bool increase)
    {
        var button = new Button
        {
            Content = glyph,
            Padding = new Thickness(0),
            FontSize = 8,
            Foreground = new SolidColorBrush(Color.FromRgb(168, 215, 239)),
            Background = new SolidColorBrush(Color.FromRgb(13, 33, 51)),
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Focusable = false,
            Template = CreateFlatButtonTemplate(0),
        };
        button.Click += (_, _) => StepInputValue(input, step, minimum, maximum, increase);
        AutomationProperties.SetName(
            button,
            increase
                ? Localize("Macro_IncreaseValue", "Aumentar valor")
                : Localize("Macro_DecreaseValue", "Diminuir valor"));
        return button;
    }

    /// <summary>Flat button chrome with a hover state; <paramref name="radius"/> rounds the corners.</summary>
    private static ControlTemplate CreateFlatButtonTemplate(double radius)
    {
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border), "Chrome");
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
        border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background")
        {
            RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent),
        });
        border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush")
        {
            RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent),
        });
        border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness")
        {
            RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent),
        });
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        // The button Padding reaches the content: the text never touches the borders.
        presenter.SetBinding(FrameworkElement.MarginProperty, new System.Windows.Data.Binding("Padding")
        {
            RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent),
        });
        border.AppendChild(presenter);
        template.VisualTree = border;

        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(UIElement.OpacityProperty, 0.86, "Chrome"));
        template.Triggers.Add(hover);
        var pressed = new Trigger { Property = System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty, Value = true };
        pressed.Setters.Add(new Setter(UIElement.OpacityProperty, 0.7, "Chrome"));
        template.Triggers.Add(pressed);
        return template;
    }

    private static void StepInputValue(
        TextBox input,
        double step,
        double minimum,
        double maximum,
        bool increase)
    {
        double current = TryParseNumber(input.Text, out double parsed) ? parsed : minimum;
        double next = Math.Clamp(
            Math.Round(current + (increase ? step : -step), 2, MidpointRounding.AwayFromZero),
            minimum,
            maximum);
        string formatted = step >= 1
            ? next.ToString("0", CultureInfo.InvariantCulture)
            : next.ToString("0.##", CultureInfo.InvariantCulture);
        input.Text = formatted;
        input.CaretIndex = formatted.Length;
        input.Focus();
    }

    /// <summary>Play/Parar: full width of its column, compact height and real inner padding.</summary>
    private static Button CreateButton(string text, Color background)
    {
        var button = new Button
        {
            Content = text,
            Height = 34,
            MinWidth = 0,
            MinHeight = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(16, 0, 16, 1),
            Background = new SolidColorBrush(background),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Cursor = Cursors.Hand,
            Template = CreateFlatButtonTemplate(8),
        };
        AutomationProperties.SetName(button, text);
        return button;
    }

    private void PlayButtonOnClick(object sender, RoutedEventArgs eventArgs) =>
        PlayRequested?.Invoke(this, EventArgs.Empty);

    private void StopButtonOnClick(object sender, RoutedEventArgs eventArgs)
    {
        StopRequested?.Invoke(this, EventArgs.Empty);
        HideSetup();
    }

    private void TargetDragDelta(object sender, DragDeltaEventArgs eventArgs)
    {
        if (_updating || !_surface.HasArea)
        {
            return;
        }

        (double scaleX, double scaleY) = GetSurfaceScale();
        if (_useLargeFrame)
        {
            // The aim is glued to the frame center: dragging it moves the frame.
            MoveRegionBy(
                eventArgs.HorizontalChange / scaleX,
                eventArgs.VerticalChange / scaleY);
            return;
        }

        MacroPoint candidate = new(
            _target.X + eventArgs.HorizontalChange / scaleX,
            _target.Y + eventArgs.VerticalChange / scaleY);
        UpdateTarget(candidate);
    }

    private void FrameDragDelta(object sender, DragDeltaEventArgs eventArgs)
    {
        if (_updating || !_surface.HasArea || !_useLargeFrame)
        {
            return;
        }

        (double scaleX, double scaleY) = GetSurfaceScale();
        MoveRegionBy(
            eventArgs.HorizontalChange / scaleX,
            eventArgs.VerticalChange / scaleY);
    }

    private void MoveRegionBy(double deltaX, double deltaY)
    {
        SurfaceRegion moved = ClampRegion(new SurfaceRegion(
            _region.X + (int)Math.Round(deltaX),
            _region.Y + (int)Math.Round(deltaY),
            _region.Width,
            _region.Height));
        if (moved == _region)
        {
            return;
        }

        _region = moved;
        _target = RegionCenter(_region);
        ApplyRegionToCanvas();
        RegionChanged?.Invoke(this, moved);
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ResizeDragDelta(object sender, DragDeltaEventArgs eventArgs)
    {
        if (_updating || !_surface.HasArea)
        {
            return;
        }

        (double scaleX, double scaleY) = GetSurfaceScale();
        UpdateRegion(new SurfaceRegion(
            _region.X,
            _region.Y,
            _region.Width + (int)Math.Round(eventArgs.HorizontalChange / scaleX),
            _region.Height + (int)Math.Round(eventArgs.VerticalChange / scaleY)));
    }

    private void UpdateTarget(MacroPoint candidate)
    {
        MacroPoint clamped = new(
            Math.Clamp(candidate.X, 0, Math.Max(0, _surface.Width - 1)),
            Math.Clamp(candidate.Y, 0, Math.Max(0, _surface.Height - 1)));
        _target = clamped;
        _region = CenterRegionOnTarget(_region.Width, _region.Height);

        ApplyRegionToCanvas();
        RegionChanged?.Invoke(this, _region);
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateRegion(SurfaceRegion candidate)
    {
        SurfaceRegion clamped = ClampRegion(candidate);
        if (clamped == _region)
        {
            return;
        }

        _region = clamped;
        _target = _useLargeFrame
            ? RegionCenter(_region)
            : ClampPointToSurface(_target, _surface);
        ApplyRegionToCanvas();
        RegionChanged?.Invoke(this, clamped);
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void LargeFrameToggleOnChanged(object sender, RoutedEventArgs eventArgs)
    {
        if (_updating || !_surface.HasArea)
        {
            return;
        }

        _useLargeFrame = _largeFrameToggle.IsChecked == true;
        if (_useLargeFrame)
        {
            // Keep the aim centered inside the frame when it is turned on.
            _target = RegionCenter(_region);
        }
        else
        {
            _region = CenterRegionOnTarget(_region.Width, _region.Height);
        }

        ApplyRegionToCanvas();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SpeedInputOnTextChanged(object sender, TextChangedEventArgs eventArgs)
    {
        if (_updating)
        {
            return;
        }

        _speed = TryParseNumber(_speedInput.Text, out double parsed)
            ? Math.Clamp(parsed, 0.25, 4.0)
            : 1.0;
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyRegionToCanvas()
    {
        if (!_surface.HasArea || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        (double scaleX, double scaleY) = GetSurfaceScale();
        _updating = true;
        try
        {
            bool showFrame = _useLargeFrame;
            bool showAim = _mode == MacroMode.Clicks;
            _frame.Visibility = showFrame ? Visibility.Visible : Visibility.Collapsed;
            _resizeThumb.Visibility = showFrame ? Visibility.Visible : Visibility.Collapsed;
            _frameMoveThumb.Visibility = showFrame ? Visibility.Visible : Visibility.Collapsed;
            if (showFrame)
            {
                double frameLeft = _region.X * scaleX;
                double frameTop = _region.Y * scaleY;
                double frameWidth = Math.Max(1, _region.Width * scaleX);
                double frameHeight = Math.Max(1, _region.Height * scaleY);
                _frame.Width = frameWidth;
                _frame.Height = frameHeight;
                Canvas.SetLeft(_frame, frameLeft);
                Canvas.SetTop(_frame, frameTop);
                _frameMoveThumb.Width = frameWidth;
                _frameMoveThumb.Height = frameHeight;
                Canvas.SetLeft(_frameMoveThumb, frameLeft);
                Canvas.SetTop(_frameMoveThumb, frameTop);
                Canvas.SetLeft(_resizeThumb, frameLeft + frameWidth - _resizeThumb.Width);
                Canvas.SetTop(_resizeThumb, frameTop + frameHeight - _resizeThumb.Height);
            }

            _targetThumb.Visibility = showAim ? Visibility.Visible : Visibility.Collapsed;
            _targetMarker.Visibility = showAim ? Visibility.Visible : Visibility.Collapsed;
            if (showAim)
            {
                Canvas.SetLeft(_targetThumb, (_target.X * scaleX) - _targetThumb.Width / 2.0);
                Canvas.SetTop(_targetThumb, (_target.Y * scaleY) - _targetThumb.Height / 2.0);
                Canvas.SetLeft(_targetMarker, (_target.X * scaleX) - 8);
                Canvas.SetTop(_targetMarker, (_target.Y * scaleY) - 16);
            }
        }
        finally
        {
            _updating = false;
        }
    }

    private (double ScaleX, double ScaleY) GetSurfaceScale()
    {
        if (_surface.Width <= 0 || _surface.Height <= 0)
        {
            return (1, 1);
        }

        // The overlay window is placed with SetWindowPos (physical pixels) while WPF
        // reports ActualWidth/ActualHeight in DIPs, so the per-monitor scale has to be
        // applied or the frame and the aim drift away from the game surface.
        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        return (
            ActualWidth * dpi.DpiScaleX / _surface.Width,
            ActualHeight * dpi.DpiScaleY / _surface.Height);
    }

    private SurfaceRegion ClampRegion(SurfaceRegion candidate)
    {
        if (!_surface.HasArea)
        {
            return candidate;
        }

        int width = Math.Clamp(candidate.Width, Math.Min(80, _surface.Width), _surface.Width);
        int height = Math.Clamp(candidate.Height, Math.Min(80, _surface.Height), _surface.Height);
        int x = Math.Clamp(candidate.X, 0, _surface.Width - width);
        int y = Math.Clamp(candidate.Y, 0, _surface.Height - height);
        return new SurfaceRegion(x, y, width, height);
    }

    private SurfaceRegion CenterRegionOnTarget(int width, int height)
    {
        int clampedWidth = Math.Clamp(width, Math.Min(80, _surface.Width), _surface.Width);
        int clampedHeight = Math.Clamp(height, Math.Min(80, _surface.Height), _surface.Height);
        return ClampRegion(new SurfaceRegion(
            (int)Math.Round(_target.X - clampedWidth / 2.0),
            (int)Math.Round(_target.Y - clampedHeight / 2.0),
            clampedWidth,
            clampedHeight));
    }

    private static MacroPoint ClampPointToSurface(MacroPoint point, SurfaceGeometry surface) =>
        new(
            Math.Clamp(point.X, 0, Math.Max(0, surface.Width - 1)),
            Math.Clamp(point.Y, 0, Math.Max(0, surface.Height - 1)));

    private static MacroPoint RegionCenter(SurfaceRegion region) =>
        new(region.X + region.Width / 2.0, region.Y + region.Height / 2.0);

    private static ControlTemplate CreateLargeFrameToggleTemplate()
    {
        var template = new ControlTemplate(typeof(ToggleButton));

        var track = new FrameworkElementFactory(typeof(Border), "Track");
        track.SetValue(Border.CornerRadiusProperty, new CornerRadius(11));
        track.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(12, 34, 52)));
        track.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(71, 119, 142)));
        track.SetValue(Border.BorderThicknessProperty, new Thickness(1));

        var knob = new FrameworkElementFactory(typeof(Border), "Knob");
        knob.SetValue(FrameworkElement.WidthProperty, 16d);
        knob.SetValue(FrameworkElement.HeightProperty, 16d);
        knob.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        knob.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(173, 194, 211)));
        knob.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        knob.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        knob.SetValue(FrameworkElement.MarginProperty, new Thickness(2, 0, 0, 0));
        track.AppendChild(knob);
        template.VisualTree = track;

        var checkedTrigger = new Trigger
        {
            Property = ToggleButton.IsCheckedProperty,
            Value = true,
        };
        checkedTrigger.Setters.Add(new Setter(
            Border.BackgroundProperty,
            new SolidColorBrush(Color.FromRgb(35, 230, 255)),
            "Track"));
        checkedTrigger.Setters.Add(new Setter(
            FrameworkElement.HorizontalAlignmentProperty,
            HorizontalAlignment.Right,
            "Knob"));
        checkedTrigger.Setters.Add(new Setter(
            FrameworkElement.MarginProperty,
            new Thickness(0, 0, 2, 0),
            "Knob"));
        checkedTrigger.Setters.Add(new Setter(
            Border.BackgroundProperty,
            new SolidColorBrush(Color.FromRgb(5, 16, 29)),
            "Knob"));
        template.Triggers.Add(checkedTrigger);

        var hoverTrigger = new Trigger
        {
            Property = UIElement.IsMouseOverProperty,
            Value = true,
        };
        hoverTrigger.Setters.Add(new Setter(
            Border.BorderBrushProperty,
            new SolidColorBrush(Color.FromRgb(35, 230, 255)),
            "Track"));
        template.Triggers.Add(hoverTrigger);

        return template;
    }

    private static SurfaceRegion DefaultRegion(SurfaceGeometry surface)
    {
        if (!surface.HasArea)
        {
            return new SurfaceRegion(0, 0, 0, 0);
        }

        int width = Math.Min(surface.Width, Math.Max(80, (int)Math.Round(surface.Width * 0.32)));
        int height = Math.Min(surface.Height, Math.Max(80, (int)Math.Round(surface.Height * 0.30)));
        return new SurfaceRegion(
            Math.Max(0, (surface.Width - width) / 2),
            Math.Max(0, (surface.Height - height) / 2),
            width,
            height);
    }
}

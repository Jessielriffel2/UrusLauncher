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
        Topmost = true;
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
            Foreground = Brushes.White,
            FontSize = 24,
            FontWeight = FontWeights.Bold,
            IsHitTestVisible = false,
            Opacity = 0.95,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _canvas.Children.Add(_targetMarker);

        var panel = new Border
        {
            Width = 300,
            Padding = new Thickness(12),
            Background = new SolidColorBrush(Color.FromArgb(242, 5, 16, 29)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(44, 96, 124)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(12),
        };
        var panelContent = new StackPanel();
        panelContent.Children.Add(new TextBlock
        {
            Text = Localize("Macro_SetupTitle", "Macro Assistant"),
            Foreground = Brushes.White,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
        });
        _hint = new TextBlock
        {
            Text = Localize("Macro_SetupHintVision", "Arraste a moldura para cobrir a área de análise."),
            Foreground = new SolidColorBrush(Color.FromRgb(173, 194, 211)),
            FontSize = 10.5,
            Margin = new Thickness(0, 4, 0, 9),
            TextWrapping = TextWrapping.Wrap,
        };
        panelContent.Children.Add(_hint);
        _status = new TextBlock
        {
            Text = Localize("Macro_SetupStatus", "Ajuste a bolinha e pressione Play."),
            Foreground = new SolidColorBrush(Color.FromRgb(255, 223, 107)),
            FontSize = 10.5,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 9),
        };
        AutomationProperties.SetName(_status, "Macro setup status");
        panelContent.Children.Add(_status);

        _clickSettingsPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(0, 0, 0, 9),
            Visibility = Visibility.Collapsed,
        };
        var countRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
        countRow.Children.Add(new TextBlock
        {
            Text = Localize("Macro_CountLabel", "Quantidade (0 = infinito)"),
            Width = 184,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 10.5,
        });
        _clickCountInput = CreateInput("0");
        countRow.Children.Add(_clickCountInput);
        _clickSettingsPanel.Children.Add(countRow);

        var intervalRow = new StackPanel { Orientation = Orientation.Horizontal };
        intervalRow.Children.Add(new TextBlock
        {
            Text = Localize("Macro_IntervalLabel", "Intervalo entre cliques (s)"),
            Width = 184,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 10.5,
        });
        _clickIntervalInput = CreateInput("0.10");
        intervalRow.Children.Add(_clickIntervalInput);
        _clickSettingsPanel.Children.Add(intervalRow);
        panelContent.Children.Add(_clickSettingsPanel);

        var speedRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 7) };
        speedRow.Children.Add(new TextBlock
        {
            Text = Localize("Macro_SpeedLabel", "Velocidade (0,25x–4x)"),
            Width = 184,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 10.5,
        });
        _speedInput = CreateInput("1.0");
        _speedInput.TextChanged += SpeedInputOnTextChanged;
        speedRow.Children.Add(_speedInput);
        panelContent.Children.Add(speedRow);

        _largeFrameToggle = new ToggleButton
        {
            Width = 40,
            Height = 22,
            Cursor = Cursors.Hand,
            Focusable = true,
            Template = CreateLargeFrameToggleTemplate(),
        };
        _largeFrameToggle.Checked += LargeFrameToggleOnChanged;
        _largeFrameToggle.Unchecked += LargeFrameToggleOnChanged;
        _largeFrameToggleRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 9),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _largeFrameToggleRow.Children.Add(_largeFrameToggle);
        _largeFrameToggleRow.Children.Add(new TextBlock
        {
            Text = Localize("Macro_LargeFrame", "Usar moldura grande"),
            Foreground = Brushes.White,
            FontSize = 10.5,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(9, 0, 0, 0),
        });
        panelContent.Children.Add(_largeFrameToggleRow);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        _playButton = CreateButton("Play", new SolidColorBrush(Color.FromRgb(34, 120, 78)));
        _playButton.Content = Localize("Macro_Play", "Play");
        _stopButton = CreateButton(
            Localize("Macro_Stop", "Parar"),
            new SolidColorBrush(Color.FromRgb(100, 42, 52)));
        _playButton.Click += PlayButtonOnClick;
        _stopButton.Click += StopButtonOnClick;
        buttons.Children.Add(_playButton);
        buttons.Children.Add(_stopButton);
        panelContent.Children.Add(buttons);
        panel.Child = panelContent;
        root.Children.Add(panel);
        Content = root;
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
        int count = int.TryParse(_clickCountInput.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int parsedCount)
            ? Math.Max(0, parsedCount)
            : 0;
        double seconds = double.TryParse(_clickIntervalInput.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double parsedSeconds)
            ? Math.Clamp(parsedSeconds, 0.01, 3600)
            : 0.10;
        return new ClickMacroSettings(count, TimeSpan.FromSeconds(seconds));
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
            _speedInput.Text = _speed.ToString("0.##", CultureInfo.CurrentCulture);
            _clickCountInput.Text = clickSettings.Count.ToString(CultureInfo.CurrentCulture);
            _clickIntervalInput.Text = clickSettings.Interval.TotalSeconds.ToString("0.###", CultureInfo.CurrentCulture);
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

    private static Button CreateButton(string text, Brush background)
    {
        var button = new Button
        {
            Content = text,
            Width = 88,
            Height = 30,
            Margin = new Thickness(0, 0, 8, 0),
            Background = background,
            Foreground = Brushes.White,
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(1),
            FontWeight = FontWeights.SemiBold,
            Cursor = Cursors.Hand,
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

        _speed = double.TryParse(
                _speedInput.Text,
                NumberStyles.Float,
                CultureInfo.CurrentCulture,
                out double parsed)
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

        return (ActualWidth / _surface.Width, ActualHeight / _surface.Height);
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

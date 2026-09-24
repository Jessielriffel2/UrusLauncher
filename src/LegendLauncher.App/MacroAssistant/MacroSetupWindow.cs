using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using LegendLauncher.App.GameHosting;

namespace LegendLauncher.App.MacroAssistant;

internal sealed class MacroSetupWindow : Window
{
    private readonly Canvas _canvas;
    private readonly Border _frame;
    private readonly Thumb _moveThumb;
    private readonly Thumb _resizeThumb;
    private readonly TextBlock _targetMarker;
    private readonly TextBlock _status;
    private readonly StackPanel _clickSettingsPanel;
    private readonly TextBox _clickCountInput;
    private readonly TextBox _clickIntervalInput;
    private readonly Button _playButton;
    private readonly Button _stopButton;
    private MacroMode _mode = MacroMode.Gems;
    private SurfaceGeometry _surface;
    private SurfaceRegion _region;
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
        _moveThumb = new Thumb
        {
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            Focusable = false,
            Cursor = Cursors.SizeAll,
        };
        _moveThumb.DragDelta += MoveDragDelta;
        _frame.Child = _moveThumb;
        _canvas.Children.Add(_frame);

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
        _targetMarker = new TextBlock
        {
            Text = "+",
            Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
            FontSize = 24,
            FontWeight = FontWeights.Bold,
            IsHitTestVisible = false,
            Opacity = 0.9,
        };
        _canvas.Children.Add(_targetMarker);

        var panel = new Border
        {
            Width = 286,
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
            Text = "Macro Assistant",
            Foreground = Brushes.White,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
        });
        panelContent.Children.Add(new TextBlock
        {
            Text = "Arraste a moldura sobre a área alvo.",
            Foreground = new SolidColorBrush(Color.FromRgb(173, 194, 211)),
            FontSize = 10.5,
            Margin = new Thickness(0, 4, 0, 9),
            TextWrapping = TextWrapping.Wrap,
        });
        _status = new TextBlock
        {
            Text = "Ajuste a moldura e pressione Play.",
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
            Text = "Quantidade (0 = infinito)",
            Width = 174,
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
            Text = "Intervalo entre cliques (s)",
            Width = 174,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 10.5,
        });
        _clickIntervalInput = CreateInput("0.10");
        intervalRow.Children.Add(_clickIntervalInput);
        _clickSettingsPanel.Children.Add(intervalRow);
        panelContent.Children.Add(_clickSettingsPanel);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        _playButton = CreateButton("Play", new SolidColorBrush(Color.FromRgb(34, 120, 78)));
        _stopButton = CreateButton("Parar", new SolidColorBrush(Color.FromRgb(100, 42, 52)));
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

    public void SetMode(MacroMode mode)
    {
        _mode = mode;
        _targetMarker.Visibility = mode == MacroMode.Clicks ? Visibility.Visible : Visibility.Collapsed;
        _clickSettingsPanel.Visibility = mode == MacroMode.Clicks ? Visibility.Visible : Visibility.Collapsed;
        SetStatus(mode == MacroMode.Clicks
            ? "Posicione o alvo e configure os cliques."
            : "Ajuste a moldura e pressione Play.");
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

    public event EventHandler? PlayRequested;

    public event EventHandler? StopRequested;

    public event EventHandler<SurfaceRegion>? RegionChanged;

    public void ShowSetup(SurfaceGeometry surface, SurfaceRegion region)
    {
        _surface = surface;
        _region = ClampRegion(region.HasArea ? region : DefaultRegion(surface));
        SetMode(_mode);
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
        _surface = surface;
        _region = ClampRegion(_region.HasArea ? _region : DefaultRegion(surface));
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

    public void SetStatus(string status) => _status.Text = status;

    public void HideSetup() => Hide();

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

    private void MoveDragDelta(object sender, DragDeltaEventArgs eventArgs)
    {
        if (_updating || !_surface.HasArea)
        {
            return;
        }

        (double scaleX, double scaleY) = GetSurfaceScale();
        UpdateRegion(new SurfaceRegion(
            _region.X + (int)Math.Round(eventArgs.HorizontalChange / scaleX),
            _region.Y + (int)Math.Round(eventArgs.VerticalChange / scaleY),
            _region.Width,
            _region.Height));
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

    private void UpdateRegion(SurfaceRegion candidate)
    {
        SurfaceRegion clamped = ClampRegion(candidate);
        if (clamped == _region)
        {
            return;
        }

        _region = clamped;
        ApplyRegionToCanvas();
        RegionChanged?.Invoke(this, clamped);
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
            _frame.Width = Math.Max(1, _region.Width * scaleX);
            _frame.Height = Math.Max(1, _region.Height * scaleY);
            Canvas.SetLeft(_frame, _region.X * scaleX);
            Canvas.SetTop(_frame, _region.Y * scaleY);
            Canvas.SetLeft(_resizeThumb, (_region.X + _region.Width - 16) * scaleX);
            Canvas.SetTop(_resizeThumb, (_region.Y + _region.Height - 16) * scaleY);
            Canvas.SetLeft(_targetMarker, (_region.X + _region.Width / 2.0 - 8) * scaleX);
            Canvas.SetTop(_targetMarker, (_region.Y + _region.Height / 2.0 - 16) * scaleY);
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

    private static SurfaceRegion DefaultRegion(SurfaceGeometry surface)
    {
        int width = Math.Max(80, (int)Math.Round(surface.Width * 0.68));
        int height = Math.Max(80, (int)Math.Round(surface.Height * 0.52));
        width = Math.Min(width, surface.Width);
        height = Math.Min(height, surface.Height);
        return new SurfaceRegion(
            Math.Max(0, (surface.Width - width) / 2),
            Math.Max(0, (surface.Height - height) / 2),
            width,
            height);
    }
}

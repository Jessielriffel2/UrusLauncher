using System.Globalization;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using LegendLauncher.App.GameHosting;

namespace LegendLauncher.App.MacroAssistant;

internal sealed class MacroOverlayWindow : Window
{
    private readonly Guid _sessionId;
    private SurfaceGeometry _geometry;
    private MacroScanResult? _result;
    private string _status = string.Empty;
    private bool _clickThroughConfigured;

    public MacroOverlayWindow(Guid sessionId, Window owner)
    {
        _sessionId = sessionId;
        Owner = owner;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        IsHitTestVisible = false;
        Focusable = false;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        SourceInitialized += OnSourceInitialized;
    }

    public Guid SessionId => _sessionId;

    public void Apply(
        SurfaceGeometry geometry,
        MacroScanResult? result,
        string status,
        bool visible)
    {
        _geometry = geometry;
        _result = result;
        _status = status ?? string.Empty;

        if (!visible || !geometry.HasArea)
        {
            Hide();
            return;
        }

        if (!IsVisible)
        {
            Show();
        }

        NativeWindowMethods.SetOverlayBounds(
            new WindowInteropHelper(this).Handle,
            geometry.X,
            geometry.Y,
            geometry.Width,
            geometry.Height);
        InvalidateVisual();
    }

    public void HideOverlay() => Hide();

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (!_geometry.HasArea || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        double scaleX = ActualWidth / _geometry.Width;
        double scaleY = ActualHeight / _geometry.Height;
        if (_result?.Move is { } move)
        {
            Point start = ToClient(move.Start, scaleX, scaleY);
            Point end = ToClient(move.End, scaleX, scaleY);
            DrawArrow(drawingContext, start, end);
        }
        else if (_result?.Action is { } action)
        {
            if (action.InputClick is { } input)
            {
                DrawCrosshair(drawingContext, ToClient(input, scaleX, scaleY), Colors.White);
            }

            if (action.Click is { } click)
            {
                Point target = ToClient(click, scaleX, scaleY);
                DrawCrosshair(drawingContext, target, Color.FromRgb(35, 230, 255));
                if (action.InputClick is { } inputPoint)
                {
                    drawingContext.DrawLine(
                        new Pen(new SolidColorBrush(Color.FromArgb(180, 35, 230, 255)), 2),
                        ToClient(inputPoint, scaleX, scaleY),
                        target);
                }
            }
            else if (action.AfterClick is { } afterClick)
            {
                DrawCrosshair(drawingContext, ToClient(afterClick, scaleX, scaleY), Color.FromRgb(255, 210, 74));
            }
        }

        DrawStatus(drawingContext);
    }

    protected override void OnClosed(EventArgs eventArgs)
    {
        SourceInitialized -= OnSourceInitialized;
        base.OnClosed(eventArgs);
    }

    private void OnSourceInitialized(object? sender, EventArgs eventArgs)
    {
        if (_clickThroughConfigured)
        {
            return;
        }

        _clickThroughConfigured = true;
        try
        {
            NativeWindowMethods.SetClickThrough(new WindowInteropHelper(this).Handle);
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }

    private void DrawArrow(DrawingContext drawingContext, Point start, Point end)
    {
        var pen = new Pen(new SolidColorBrush(Color.FromRgb(255, 210, 74)), 3);
        drawingContext.DrawLine(pen, start, end);
        drawingContext.DrawEllipse(
            new SolidColorBrush(Color.FromArgb(90, 255, 210, 74)),
            pen,
            start,
            7,
            7);
        drawingContext.DrawEllipse(
            new SolidColorBrush(Color.FromArgb(90, 35, 230, 255)),
            new Pen(new SolidColorBrush(Color.FromRgb(35, 230, 255)), 3),
            end,
            8,
            8);

        Vector direction = end - start;
        if (direction.Length < 1)
        {
            return;
        }

        direction /= direction.Length;
        Vector normal = new(-direction.Y, direction.X);
        Point tip = end;
        Point left = tip - direction * 16 + normal * 7;
        Point right = tip - direction * 16 - normal * 7;
        drawingContext.DrawLine(pen, tip, left);
        drawingContext.DrawLine(pen, tip, right);
    }

    private static void DrawCrosshair(DrawingContext drawingContext, Point point, Color color)
    {
        var brush = new SolidColorBrush(color);
        drawingContext.DrawEllipse(new SolidColorBrush(Color.FromArgb(70, color.R, color.G, color.B)), new Pen(brush, 2), point, 7, 7);
        drawingContext.DrawLine(new Pen(brush, 2), point + new Vector(-13, 0), point + new Vector(13, 0));
        drawingContext.DrawLine(new Pen(brush, 2), point + new Vector(0, -13), point + new Vector(0, 13));
    }

    private void DrawStatus(DrawingContext drawingContext)
    {
        if (string.IsNullOrWhiteSpace(_status))
        {
            return;
        }

        var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        FormattedText text = new(
            _status,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            typeface,
            11,
            Brushes.White,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        var background = new SolidColorBrush(Color.FromArgb(185, 5, 16, 29));
        drawingContext.DrawRoundedRectangle(background, null, new Rect(12, 12, text.Width + 18, text.Height + 10), 6, 6);
        drawingContext.DrawText(text, new Point(21, 17));
    }

    private static Point ToClient(MacroPoint point, double scaleX, double scaleY) =>
        new(point.X * scaleX, point.Y * scaleY);
}

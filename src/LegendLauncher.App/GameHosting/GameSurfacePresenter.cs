using System.Windows;
using System.Windows.Controls;

namespace LegendLauncher.App.GameHosting;

internal sealed class GameSurfacePresenter : ContentControl
{
    public static readonly DependencyProperty AttachmentProperty = DependencyProperty.Register(
        nameof(Attachment),
        typeof(GameWindowAttachment),
        typeof(GameSurfacePresenter),
        new PropertyMetadata(null, AttachmentOnChanged));

    public GameSurfacePresenter()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
    }

    public GameWindowAttachment? Attachment
    {
        get => (GameWindowAttachment?)GetValue(AttachmentProperty);
        set => SetValue(AttachmentProperty, value);
    }

    private static void AttachmentOnChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        var presenter = (GameSurfacePresenter)dependencyObject;
        presenter.Content = eventArgs.NewValue is GameWindowAttachment attachment
            ? new EmbeddedGameSurfaceHost(attachment)
            : null;
    }
}

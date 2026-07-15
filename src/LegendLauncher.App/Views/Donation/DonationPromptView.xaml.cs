using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace LegendLauncher.App.Views.Donation;

public partial class DonationPromptView : UserControl
{
    internal const string PixCnpj = "57.646.942/0001-69";

    public DonationPromptView()
    {
        InitializeComponent();
        IsVisibleChanged += DonationPromptView_OnIsVisibleChanged;
    }

    internal static void CopyPixCnpj(Action<string> clipboardWriter)
    {
        ArgumentNullException.ThrowIfNull(clipboardWriter);
        clipboardWriter(PixCnpj);
    }

    private void CopyPixButton_OnClick(object sender, RoutedEventArgs eventArgs)
    {
        PixCopySuccessText.Visibility = Visibility.Collapsed;
        PixCopyFailureText.Visibility = Visibility.Collapsed;
        try
        {
            CopyPixCnpj(Clipboard.SetText);
            PixCopySuccessText.Visibility = Visibility.Visible;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            PixCopyFailureText.Visibility = Visibility.Visible;
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            PixCopyFailureText.Visibility = Visibility.Visible;
        }
    }

    private void DonationPromptView_OnIsVisibleChanged(
        object sender,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.NewValue is not true)
        {
            return;
        }

        PixCopySuccessText.Visibility = Visibility.Collapsed;
        PixCopyFailureText.Visibility = Visibility.Collapsed;

        Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            () => ClosePromptButton.Focus());
    }
}

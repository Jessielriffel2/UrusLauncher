using System.Security.Cryptography;
using System.Xml.Linq;
using LegendLauncher.App.Views.Donation;

namespace LegendLauncher.Tests.App;

public sealed class DonationPromptAssetTests
{
    private const string ExpectedQrSha256 =
        "EADCCECE3D8D2EC926C81AF0386A169178FA0795D6BADF7FC90794648601C6FC";
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml =
        "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly XNamespace Shell =
        "clr-namespace:System.Windows.Shell;assembly=PresentationFramework";

    [Fact]
    public void QrAsset_IsTheExactUserSuppliedImageAndIsPackagedAsWpfResource()
    {
        string root = FindRepositoryRoot();
        string assetPath = Path.Combine(
            root,
            "src",
            "LegendLauncher.App",
            "Assets",
            "paypal-donation-qr.jpeg");
        byte[] asset = File.ReadAllBytes(assetPath);

        Assert.Equal(62_216, asset.Length);
        Assert.Equal(ExpectedQrSha256, Convert.ToHexString(SHA256.HashData(asset)));

        XDocument project = XDocument.Load(Path.Combine(
            root,
            "src",
            "LegendLauncher.App",
            "LegendLauncher.App.csproj"));
        Assert.Contains(
            project.Descendants("Resource"),
            element => string.Equals(
                element.Attribute("Include")?.Value,
                "Assets\\paypal-donation-qr.jpeg",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Modal_KeepsTheQrSquareUnclippedInsideAPaddedWhitePanel()
    {
        XDocument document = XDocument.Load(FindRepositoryFile(
            "src",
            "LegendLauncher.App",
            "Views",
            "Donation",
            "DonationPromptView.xaml"));
        XElement image = document
            .Descendants(Presentation + "Image")
            .Single(element => element.Attribute("Source")?.Value?.Contains(
                "paypal-donation-qr.jpeg",
                StringComparison.Ordinal) == true);

        Assert.Equal("300", image.Attribute("Width")?.Value);
        Assert.Equal("300", image.Attribute("Height")?.Value);
        Assert.Equal("Uniform", image.Attribute("Stretch")?.Value);
        Assert.Equal("NearestNeighbor", image.Attribute("RenderOptions.BitmapScalingMode")?.Value);
        Assert.Null(image.Attribute("Clip"));
        Assert.Null(image.Attribute("ClipToBounds"));
        Assert.Null(image.Attribute("OpacityMask"));

        XElement quietPanel = Assert.IsType<XElement>(image.Parent);
        Assert.Equal(Presentation + "Border", quietPanel.Name);
        Assert.Equal("White", quietPanel.Attribute("Background")?.Value);
        Assert.Equal("14", quietPanel.Attribute("Padding")?.Value);
    }

    [Fact]
    public void MainWindow_ProvidesManualPayPalEntryAndATopmostModalOverlay()
    {
        XDocument document = XDocument.Load(FindRepositoryFile(
            "src",
            "LegendLauncher.App",
            "MainWindow.xaml"));
        XElement button = FindNamedElement(document, "Button", "DonationHeaderButton");
        XElement overlay = document
            .Descendants()
            .Single(element => element.Attribute(Xaml + "Name")?.Value ==
                "DonationPromptOverlay");

        Assert.Equal("{Binding OpenDonationPromptCommand}", button.Attribute("Command")?.Value);
        Assert.Equal(
            "True",
            button.Attribute(Shell + "WindowChrome.IsHitTestVisibleInChrome")?.Value);
        Assert.Equal("500", overlay.Attribute("Panel.ZIndex")?.Value);
        Assert.Contains("IsDonationPromptVisible", overlay.Attribute("Visibility")?.Value);
        Assert.Same(overlay, overlay.Parent?.Elements().Last());
    }

    [Fact]
    public void Modal_IsRoundedKeyboardDismissibleAndCyclesFocus()
    {
        XDocument document = XDocument.Load(FindRepositoryFile(
            "src",
            "LegendLauncher.App",
            "Views",
            "Donation",
            "DonationPromptView.xaml"));
        XElement root = Assert.IsType<XElement>(document.Root);
        XElement card = document
            .Descendants(Presentation + "Border")
            .Single(element => element.Attribute("MaxWidth")?.Value == "930");
        XElement escape = document
            .Descendants(Presentation + "KeyBinding")
            .Single(element => element.Attribute("Key")?.Value == "Escape");

        Assert.Equal("Cycle", root.Attribute("KeyboardNavigation.TabNavigation")?.Value);
        Assert.Equal("26", card.Attribute("CornerRadius")?.Value);
        Assert.Equal("{Binding CloseDonationPromptCommand}", escape.Attribute("Command")?.Value);
        Assert.Equal(
            2,
            document.Descendants(Presentation + "Button").Count(element =>
                element.Attribute("Command")?.Value == "{Binding CloseDonationPromptCommand}"));
    }

    [Fact]
    public void Modal_ProvidesAccessibleBrazilianPixCopyWithoutChangingPayPalQr()
    {
        XDocument document = XDocument.Load(FindRepositoryFile(
            "src",
            "LegendLauncher.App",
            "Views",
            "Donation",
            "DonationPromptView.xaml"));
        XElement panel = FindNamedElement(document, "Border", "PixDonationPanel");
        XElement key = FindNamedElement(document, "TextBox", "PixCnpjText");
        XElement copy = FindNamedElement(document, "Button", "CopyPixButton");
        XElement success = FindNamedElement(document, "TextBlock", "PixCopySuccessText");
        XElement failure = FindNamedElement(document, "TextBlock", "PixCopyFailureText");

        Assert.Equal(DonationPromptView.PixCnpj, key.Attribute("Text")?.Value);
        Assert.Contains(
            panel.Descendants(Presentation + "TextBlock"),
            element => element.Attribute("Text")?.Value ==
                "{loc:Localize Donation_PixTitle}");
        Assert.Equal("True", key.Attribute("IsReadOnly")?.Value);
        Assert.Equal(
            "{loc:Localize Donation_PixKeyAutomation}",
            key.Attribute("AutomationProperties.Name")?.Value);
        Assert.Equal("CopyPixButton_OnClick", copy.Attribute("Click")?.Value);
        Assert.Equal("{loc:Localize Donation_PixCopy}", copy.Attribute("Content")?.Value);
        Assert.Equal(
            "{loc:Localize Donation_PixCopyAutomation}",
            copy.Attribute("AutomationProperties.Name")?.Value);
        Assert.Equal(
            "{loc:Localize Donation_PixCopyHelp}",
            copy.Attribute("AutomationProperties.HelpText")?.Value);
        Assert.Equal("Polite", success.Attribute("AutomationProperties.LiveSetting")?.Value);
        Assert.Equal("Assertive", failure.Attribute("AutomationProperties.LiveSetting")?.Value);

        XElement qr = document
            .Descendants(Presentation + "Image")
            .Single(element => element.Attribute("Source")?.Value?.Contains(
                "paypal-donation-qr.jpeg",
                StringComparison.Ordinal) == true);
        Assert.Equal("300", qr.Attribute("Width")?.Value);
        Assert.Equal("300", qr.Attribute("Height")?.Value);
    }

    [Fact]
    public void CopyPixCnpj_WritesTheExactRequestedKey()
    {
        string? copied = null;

        DonationPromptView.CopyPixCnpj(value => copied = value);

        Assert.Equal("57.646.942/0001-69", copied);
        Assert.Throws<ArgumentNullException>(() => DonationPromptView.CopyPixCnpj(null!));
    }

    private static XElement FindNamedElement(
        XDocument document,
        string localName,
        string name) =>
        document
            .Descendants(Presentation + localName)
            .Single(element => element.Attribute(Xaml + "Name")?.Value == name);

    private static string FindRepositoryFile(params string[] relativeSegments) =>
        Path.Combine([FindRepositoryRoot(), .. relativeSegments]);

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LegendLauncherNext.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("LegendLauncherNext repository root was not found.");
    }
}

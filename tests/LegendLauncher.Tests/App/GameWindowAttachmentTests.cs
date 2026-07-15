using LegendLauncher.App.GameHosting;

namespace LegendLauncher.Tests.App;

public sealed class GameWindowAttachmentTests
{
    [Fact]
    public void EmbeddedStyle_AddsChildClippingAndRemovesTopLevelChrome()
    {
        const uint unrelatedStyle = 0x00000040;
        uint originalStyle =
            NativeWindowMethods.WindowStylePopup |
            NativeWindowMethods.WindowStyleCaption |
            NativeWindowMethods.WindowStyleThickFrame |
            NativeWindowMethods.WindowStyleSystemMenu |
            NativeWindowMethods.WindowStyleMinimizeBox |
            NativeWindowMethods.WindowStyleMaximizeBox |
            unrelatedStyle;

        uint embeddedStyle = NativeWindowMethods.CalculateEmbeddedStyle(originalStyle);

        Assert.NotEqual(0u, embeddedStyle & NativeWindowMethods.WindowStyleChild);
        Assert.NotEqual(0u, embeddedStyle & NativeWindowMethods.WindowStyleClipChildren);
        Assert.NotEqual(0u, embeddedStyle & NativeWindowMethods.WindowStyleClipSiblings);
        Assert.Equal(0u, embeddedStyle & NativeWindowMethods.WindowStylePopup);
        Assert.Equal(0u, embeddedStyle & NativeWindowMethods.WindowStyleCaption);
        Assert.Equal(0u, embeddedStyle & NativeWindowMethods.WindowStyleThickFrame);
        Assert.Equal(0u, embeddedStyle & NativeWindowMethods.WindowStyleSystemMenu);
        Assert.Equal(0u, embeddedStyle & NativeWindowMethods.WindowStyleMinimizeBox);
        Assert.Equal(0u, embeddedStyle & NativeWindowMethods.WindowStyleMaximizeBox);
        Assert.NotEqual(0u, embeddedStyle & unrelatedStyle);
    }

    [Fact]
    public void EmbeddedStyle_IsIdempotent()
    {
        const uint originalStyle = 0x90CF0000;

        uint firstPass = NativeWindowMethods.CalculateEmbeddedStyle(originalStyle);
        uint secondPass = NativeWindowMethods.CalculateEmbeddedStyle(firstPass);

        Assert.Equal(firstPass, secondPass);
    }

    [Theory]
    [InlineData(0L, 42, true, 42u, typeof(ArgumentException))]
    [InlineData(123L, 0, true, 42u, typeof(ArgumentOutOfRangeException))]
    [InlineData(123L, -1, true, 42u, typeof(ArgumentOutOfRangeException))]
    [InlineData(123L, 42, false, 42u, typeof(ArgumentException))]
    [InlineData(123L, 42, true, 0u, typeof(InvalidOperationException))]
    [InlineData(123L, 42, true, 43u, typeof(InvalidOperationException))]
    public void ExternalWindowValidation_RejectsInvalidIdentity(
        long windowValue,
        int expectedProcessId,
        bool windowExists,
        uint actualProcessId,
        Type expectedExceptionType)
    {
        Exception exception = Assert.Throws(
            expectedExceptionType,
            () => GameWindowAttachment.ValidateExternalWindowIdentity(
                new nint(windowValue),
                expectedProcessId,
                windowExists,
                actualProcessId));

        Assert.DoesNotContain(windowValue.ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExternalWindowValidation_AcceptsMatchingProcess()
    {
        GameWindowAttachment.ValidateExternalWindowIdentity(
            new nint(123),
            expectedProcessId: 42,
            windowExists: true,
            actualProcessId: 42);
    }

    [Theory]
    [InlineData(0L, true, 42u, 42u)]
    [InlineData(123L, false, 42u, 42u)]
    [InlineData(123L, true, 42u, 0u)]
    [InlineData(123L, true, 43u, 42u)]
    public void ProxyValidation_RejectsHandlesOutsideLauncherProcess(
        long proxyValue,
        bool windowExists,
        uint actualProcessId,
        uint launcherProcessId)
    {
        Assert.Throws<ArgumentException>(() =>
            GameWindowAttachment.ValidateProxyWindowIdentity(
                new nint(proxyValue),
                windowExists,
                actualProcessId,
                launcherProcessId));
    }

    [Fact]
    public void ProxyValidation_AcceptsLauncherOwnedWindow()
    {
        GameWindowAttachment.ValidateProxyWindowIdentity(
            new nint(123),
            windowExists: true,
            actualProcessId: 42,
            launcherProcessId: 42);
    }

    [Theory]
    [InlineData(0L, 0L, false)]
    [InlineData(0L, 200L, false)]
    [InlineData(100L, 200L, false)]
    [InlineData(200L, 200L, true)]
    public void DetachGuard_RequiresTheExactNonZeroProxy(
        long proxyValue,
        long currentParentValue,
        bool expected)
    {
        bool result = GameWindowAttachment.ShouldDetachFromProxy(
            new nint(proxyValue),
            new nint(currentParentValue));

        Assert.Equal(expected, result);
    }
}

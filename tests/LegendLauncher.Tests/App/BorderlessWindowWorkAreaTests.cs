using LegendLauncher.App;

namespace LegendLauncher.Tests.App;

public sealed class BorderlessWindowWorkAreaTests
{
    [Fact]
    public void MaximizedLayoutUsesTaskbarAwareWorkAreaOnAnyMonitorOrigin()
    {
        var monitor = new BorderlessWindowWorkArea.NativeRect(-1920, 0, 0, 1080);
        var workArea = new BorderlessWindowWorkArea.NativeRect(-1920, 0, 0, 1040);

        BorderlessWindowWorkArea.MaximizedLayout layout =
            BorderlessWindowWorkArea.CalculateMaximizedLayout(monitor, workArea);

        Assert.Equal(new BorderlessWindowWorkArea.NativePoint(0, 0), layout.Position);
        Assert.Equal(new BorderlessWindowWorkArea.NativePoint(1920, 1040), layout.Size);
    }

    [Fact]
    public void MaximizedLayoutOffsetsForTaskbarsAlongTopAndLeftEdges()
    {
        var monitor = new BorderlessWindowWorkArea.NativeRect(0, 0, 2560, 1440);
        var workArea = new BorderlessWindowWorkArea.NativeRect(48, 36, 2560, 1440);

        BorderlessWindowWorkArea.MaximizedLayout layout =
            BorderlessWindowWorkArea.CalculateMaximizedLayout(monitor, workArea);

        Assert.Equal(new BorderlessWindowWorkArea.NativePoint(48, 36), layout.Position);
        Assert.Equal(new BorderlessWindowWorkArea.NativePoint(2512, 1404), layout.Size);
    }

    [Fact]
    public void MaximizedLayoutSupportsMonitorAbovePrimaryDisplay()
    {
        var monitor = new BorderlessWindowWorkArea.NativeRect(0, -1440, 2560, 0);
        var workArea = new BorderlessWindowWorkArea.NativeRect(0, -1440, 2560, -40);

        BorderlessWindowWorkArea.MaximizedLayout layout =
            BorderlessWindowWorkArea.CalculateMaximizedLayout(monitor, workArea);

        Assert.Equal(new BorderlessWindowWorkArea.NativePoint(0, 0), layout.Position);
        Assert.Equal(new BorderlessWindowWorkArea.NativePoint(2560, 1400), layout.Size);
    }

    [Theory]
    [InlineData(120u, 1040, 832, 700)]
    [InlineData(144u, 1032, 688, 688)]
    public void NormalLimitsConvertPixelsToDipAndClampOversizedHeight(
        uint dpi,
        int physicalWorkAreaHeight,
        double expectedMaxHeight,
        double expectedMinHeight)
    {
        BorderlessWindowWorkArea.NormalWindowLimits limits =
            BorderlessWindowWorkArea.CalculateNormalWindowLimits(
                new BorderlessWindowWorkArea.NativePoint(2560, physicalWorkAreaHeight),
                dpi,
                originalMinWidth: 1180,
                originalMinHeight: 700,
                originalMaxWidth: double.PositiveInfinity,
                originalMaxHeight: double.PositiveInfinity);

        Assert.Equal(expectedMaxHeight, limits.MaxHeight, precision: 6);
        Assert.Equal(expectedMinHeight, limits.MinHeight, precision: 6);
    }

    [Fact]
    public void NormalLimitsRestoreOriginalMinimumOnLargerMonitor()
    {
        BorderlessWindowWorkArea.NormalWindowLimits limits =
            BorderlessWindowWorkArea.CalculateNormalWindowLimits(
                new BorderlessWindowWorkArea.NativePoint(2560, 1400),
                dpi: 96,
                originalMinWidth: 1180,
                originalMinHeight: 700,
                originalMaxWidth: double.PositiveInfinity,
                originalMaxHeight: double.PositiveInfinity);

        Assert.Equal(700, limits.MinHeight);
        Assert.Equal(1400, limits.MaxHeight);
    }

    [Theory]
    [InlineData(BorderlessWindowWorkArea.SettingChangeMessage, true)]
    [InlineData(BorderlessWindowWorkArea.DisplayChangeMessage, true)]
    [InlineData(BorderlessWindowWorkArea.DpiChangedMessage, true)]
    [InlineData(0x0024, false)]
    [InlineData(0x000F, false)]
    public void RefreshMessageClassificationMatchesDisplayAndWorkAreaChanges(
        int message,
        bool expected)
    {
        Assert.Equal(expected, BorderlessWindowWorkArea.RequiresNormalLimitsRefresh(message));
    }
}

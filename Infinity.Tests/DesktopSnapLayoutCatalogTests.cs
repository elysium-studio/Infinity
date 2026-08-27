using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopSnapLayoutCatalogTests
{
    private readonly DesktopSnapLayoutCatalog catalog = new();

    [Theory]
    [InlineData(DesktopSnapLayoutKind.Halves, 2)]
    [InlineData(DesktopSnapLayoutKind.Thirds, 3)]
    [InlineData(DesktopSnapLayoutKind.Quarters, 4)]
    [InlineData(DesktopSnapLayoutKind.MainAndStack, 3)]
    [InlineData(DesktopSnapLayoutKind.PrimaryAndSide, 2)]
    [InlineData(DesktopSnapLayoutKind.PriorityColumns, 3)]
    [InlineData(DesktopSnapLayoutKind.FourColumns, 4)]
    [InlineData(DesktopSnapLayoutKind.WidePriorityColumns, 4)]
    [InlineData(DesktopSnapLayoutKind.MainAndGrid, 5)]
    [InlineData(DesktopSnapLayoutKind.Rows, 2)]
    [InlineData(DesktopSnapLayoutKind.PrimaryAndBottom, 2)]
    [InlineData(DesktopSnapLayoutKind.ThirdRows, 3)]
    [InlineData(DesktopSnapLayoutKind.MainAndBottomStack, 3)]
    public void PresetsExposeExpectedSlots(DesktopSnapLayoutKind layout, int expectedSlots)
    {
        DesktopSnapLayoutDefinition? definition = catalog.Get(layout);

        Assert.NotNull(definition);
        Assert.Equal(expectedSlots, definition.Slots.Count);
    }

    [Theory]
    [InlineData(DesktopSnapLayoutKind.Halves, 0.25, 0.5, 0)]
    [InlineData(DesktopSnapLayoutKind.Halves, 0.75, 0.5, 1)]
    [InlineData(DesktopSnapLayoutKind.Quarters, 0.25, 0.75, 2)]
    [InlineData(DesktopSnapLayoutKind.MainAndStack, 0.9, 0.75, 2)]
    [InlineData(DesktopSnapLayoutKind.PrimaryAndSide, 0.8, 0.5, 1)]
    [InlineData(DesktopSnapLayoutKind.PriorityColumns, 0.5, 0.5, 1)]
    public void HitTestFindsSlotAtPointerPosition(DesktopSnapLayoutKind layout, double x, double y, int expectedSlot)
    {
        Assert.Equal(expectedSlot, catalog.HitTest(layout, x, y));
    }

    [Fact]
    public void NoLayoutHasNoDefinitionOrHitTarget()
    {
        Assert.Null(catalog.Get(DesktopSnapLayoutKind.None));
        Assert.Equal(-1, catalog.HitTest(DesktopSnapLayoutKind.None, 0.5, 0.5));
    }

    [Theory]
    [InlineData(1024, 768, 1, 2)]
    [InlineData(1366, 768, 1, 4)]
    [InlineData(1920, 1080, 1, 6)]
    [InlineData(3440, 1440, 1, 9)]
    [InlineData(3840, 1600, 2, 6)]
    [InlineData(1080, 1920, 1, 4)]
    public void AvailableLayoutsFollowEffectiveDisplayGeometry(double width, double height, double scale, int expectedLayouts)
    {
        Assert.Equal(expectedLayouts, catalog.GetAvailable(width, height, scale).Count);
    }

    [Fact]
    public void ThreeColumnLayoutsRequire1920EffectivePixels()
    {
        Assert.DoesNotContain(catalog.GetAvailable(2879, 1620, 1.5), definition => definition.Kind == DesktopSnapLayoutKind.Thirds);
        Assert.Contains(catalog.GetAvailable(3840, 2160, 2), definition => definition.Kind == DesktopSnapLayoutKind.Thirds);
    }
}

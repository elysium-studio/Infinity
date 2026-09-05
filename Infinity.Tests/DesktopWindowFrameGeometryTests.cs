using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopWindowFrameGeometryTests
{
    [Fact]
    public void ConvertsAsymmetricInsetsWithoutChangingVisibleBounds()
    {
        TestWindowFrameGeometryReader reader = new();
        reader.Insets[1] = (8, 1, 6, 9);
        DesktopWindowFrameGeometry geometry = new(reader);
        DesktopSnapPlacement visible = new(-1920, 40, 960, 1040);
        DesktopSnapPlacement outer = geometry.ToOuter(1, visible);
        Assert.Equal(new(-1928, 39, 974, 1050), outer);
        Assert.Equal(visible, geometry.ToVisible(1, outer));
    }


    [Fact]
    public void UnavailableOrInconsistentFrameBoundsLeaveGeometryUntouched()
    {
        TestWindowFrameGeometryReader reader = new();
        DesktopWindowFrameGeometry geometry = new(reader);
        DesktopSnapPlacement bounds = new(20, 30, 600, 400);
        Assert.Equal(bounds, geometry.ToOuter(1, bounds));
        reader.Insets[1] = (-8, 0, 8, 8);
        Assert.Equal(bounds, geometry.ToOuter(1, bounds));
    }


    [Fact]
    public void InsetsAreReadAgainAfterNativeFrameChanges()
    {
        TestWindowFrameGeometryReader reader = new();
        DesktopWindowFrameGeometry geometry = new(reader);
        DesktopSnapPlacement bounds = new(20, 30, 600, 400);
        reader.Insets[1] = (0, 0, 0, 0);
        Assert.Equal(bounds, geometry.ToOuter(1, bounds));
        reader.Insets[1] = (8, 0, 8, 8);
        Assert.Equal(new(12, 30, 616, 408), geometry.ToOuter(1, bounds));
    }
}

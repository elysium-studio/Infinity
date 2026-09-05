using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopWallpaperPlacementCalculatorTests
{
    private readonly DesktopWallpaperPlacementCalculator calculator = new();

    [Fact]
    public void TopTaskbarOffsetsFullMonitorWallpaperAboveWorkArea()
    {
        DesktopWallpaperPlacement placement = calculator.Calculate(0, 0, 3440, 1440, 0, 48);
        Assert.Equal(new DesktopWallpaperPlacement(3440, 1440, 0, -48), placement);
    }


    [Fact]
    public void BottomTaskbarKeepsFullMonitorWallpaperAtWorkAreaOrigin()
    {
        DesktopWallpaperPlacement placement = calculator.Calculate(0, 0, 3440, 1440, 0, 0);
        Assert.Equal(new DesktopWallpaperPlacement(3440, 1440, 0, 0), placement);
    }


    [Fact]
    public void LeftTaskbarOffsetsFullMonitorWallpaperLeftOfWorkArea()
    {
        DesktopWallpaperPlacement placement = calculator.Calculate(1920, 200, 2560, 1440, 1968, 200);
        Assert.Equal(new DesktopWallpaperPlacement(2560, 1440, -48, 0), placement);
    }


    [Fact]
    public void RightTaskbarKeepsFullMonitorWallpaperAtWorkAreaOrigin()
    {
        DesktopWallpaperPlacement placement = calculator.Calculate(-2560, 100, 2560, 1440, -2560, 100);
        Assert.Equal(new DesktopWallpaperPlacement(2560, 1440, 0, 0), placement);
    }


    [Fact]
    public void InvalidMonitorGeometryDoesNotProduceAPlacement()
    {
        DesktopWallpaperPlacement placement = calculator.Calculate(0, 0, 0, 1440, 0, 0);
        Assert.False(placement.IsValid);
    }
}

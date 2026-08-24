using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopPageLayoutCalculatorTests
{
    private readonly DesktopPageLayoutCalculator calculator = new();

    [Fact]
    public void VisiblePageCapacityIncludesViewportAndOverscan()
    {
        Assert.Equal(7, calculator.CalculateVisiblePageCapacity(0.38));
    }

    [Fact]
    public void VisiblePageRangeIncludesViewportAndOverscan()
    {
        Assert.Equal((0, 2), calculator.CalculateVisiblePageRange(null, 0, 1920, 0.38));
    }

    [Fact]
    public void FixedPageRangeUsesConfiguredLimit()
    {
        Assert.Equal((8, 9), calculator.CalculateVisiblePageRange(10, 19200, 1920, 0.38));
    }

    [Fact]
    public void VisiblePageRangeFollowsLivePresentationOffsetWithoutGrowing()
    {
        (int firstPage, int lastPage) = calculator.CalculateVisiblePageRange(null,
            19200,
            1920,
            0.38);

        Assert.Equal((8, 12), (firstPage, lastPage));
        Assert.Equal(5, lastPage - firstPage + 1);
    }

    [Fact]
    public void VisiblePageRangeRemainsBoundedAtLargeOffsets()
    {
        (int firstPage, int lastPage) = calculator.CalculateVisiblePageRange(null,
            19_200_000,
            1920,
            0.38);

        Assert.True(firstPage > 9000);
        Assert.InRange(lastPage - firstPage + 1, 5, 6);
    }

    [Fact]
    public void PageSpacingCanCollapseForNativeWindowHandoff()
    {
        const double width = 1920;
        Assert.Equal(width, calculator.CalculatePageX(1, width, 0, 0));
        Assert.Equal(0, calculator.CalculatePageX(1, width, width, 0));
    }

    [Fact]
    public void WindowSpacingCanCollapseForNativeWindowHandoff()
    {
        const double width = 1920;
        double x = calculator.CalculateWindowX(2000,
            2000,
            800,
            0,
            width,
            0,
            false,
            0);

        Assert.Equal(2000, x);
    }

    [Fact]
    public void WindowReceivesSameGapOffsetAsItsPage()
    {
        const double width = 1920;
        double x = calculator.CalculateWindowX(2000,
            2000,
            800,
            0,
            width,
            0,
            false);

        Assert.Equal(2064, x);
    }
}
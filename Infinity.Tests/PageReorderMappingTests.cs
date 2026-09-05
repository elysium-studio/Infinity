using Infinity.Application.Abstractions;
using Infinity.Shell;

namespace Infinity.Tests;

public sealed class PageReorderMappingTests
{
    [Fact]
    public void WindowPageUsesItsHorizontalCenter()
    {
        TrackedWindow window = new()
        {
            Handle = new(1),
            CanvasX = 1800,
            CanvasY = 0,
            Width = 400,
            Height = 300
        };
        Assert.Equal(1, PageReorderMapping.GetPage(window, 1920));
    }


    [Theory]
    [InlineData(1, 1, 3, 3)]
    [InlineData(2, 1, 3, 1)]
    [InlineData(3, 1, 3, 2)]
    [InlineData(4, 1, 3, 4)]
    public void MovingPageRightClosesGap(int page, int sourcePage, int targetPage, int expectedPage) => Assert.Equal(expectedPage, PageReorderMapping.Map(page, sourcePage, targetPage));

    [Theory]
    [InlineData(3, 3, 1, 1)]
    [InlineData(1, 3, 1, 2)]
    [InlineData(2, 3, 1, 3)]
    [InlineData(0, 3, 1, 0)]
    public void MovingPageLeftOpensGap(int page, int sourcePage, int targetPage, int expectedPage) => Assert.Equal(expectedPage, PageReorderMapping.Map(page, sourcePage, targetPage));

    [Theory]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(8, 7)]
    public void ScrollingRightClosesSourceGapWithoutOpeningDestinationGap(int page, int expectedPage)
    {
        DesktopPageReorderPreviewState state = new(1, 4, 6000, IsGapOpen: false);
        Assert.Equal(expectedPage, state.MapPage(page));
    }


    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    public void ScrollingLeftClosesSourceGapWithoutOpeningDestinationGap(int page, int expectedPage)
    {
        DesktopPageReorderPreviewState state = new(3, 0, -6000, IsGapOpen: false);
        Assert.Equal(expectedPage, state.MapPage(page));
    }
}

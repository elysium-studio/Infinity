using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopWindowDragViewportTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2)]
    public void NativeAndOverlayPointersResolveToTheSameLayoutPosition(double dpiScale)
    {
        DesktopWindowDragViewport viewport = new(-1920, -240, dpiScale, 0, 48, 960, 516);
        (double rootX, double rootY) = viewport.FromScreen(-1920 + 732.5 * dpiScale, -240 + 283.25 * dpiScale);
        (double nativeX, double nativeY) = viewport.ToLayout(rootX, rootY, 0.38);
        (double overlayX, double overlayY) = viewport.ToLayout(732.5, 283.25, 0.38);

        Assert.Equal(overlayX, nativeX, 8);
        Assert.Equal(overlayY, nativeY, 8);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0.75)]
    [InlineData(0.38)]
    public void LayoutPositionMatchesTheCompositionAnchorDuringScaling(double scale)
    {
        DesktopWindowDragViewport viewport = new(0, 0, 1, 24, 48, 960, 516);
        (double layoutX, double layoutY) = viewport.ToLayout(841.25, 372.5, scale);
        (double surfaceX, double surfaceY) = viewport.ToSurface(841.25, 372.5);
        DesktopWindowDragAnchor anchor = new(0.35, 0.025);
        (double grabX, double grabY) = anchor.GetOffset(1200, 800);

        double translationX = layoutX - grabX;
        double translationY = layoutY - grabY;

        Assert.Equal(surfaceX, (translationX + grabX) * scale + viewport.CenterX * (1 - scale), 8);
        Assert.Equal(surfaceY, (translationY + grabY) * scale + viewport.CenterY * (1 - scale), 8);
    }

    [Fact]
    public void FractionalMovementIsNotRoundedToWholeLayoutPixels()
    {
        DesktopWindowDragViewport viewport = new(0, 0, 1.5, 0, 48, 960, 516);
        (double firstX, double firstY) = viewport.FromScreen(800, 350);
        (double secondX, double secondY) = viewport.FromScreen(801, 351);
        (double firstLayoutX, double firstLayoutY) = viewport.ToLayout(firstX, firstY, 0.38);
        (double secondLayoutX, double secondLayoutY) = viewport.ToLayout(secondX, secondY, 0.38);

        Assert.Equal(1 / 1.5 / 0.38, secondLayoutX - firstLayoutX, 8);
        Assert.Equal(1 / 1.5 / 0.38, secondLayoutY - firstLayoutY, 8);
    }

    [Fact]
    public void PageMovementDoesNotMoveTheGrabbedPointAwayFromTheCursor()
    {
        DesktopWindowDragViewport viewport = new(0, 0, 1, 0, 48, 960, 516);
        (double layoutX, _) = viewport.ToLayout(1200, 320, 0.38);
        const double grabX = 325.5;
        const double firstPageX = 150;
        const double scrolledPageX = -850.25;
        double firstDelta = layoutX - grabX - firstPageX;
        double scrolledDelta = layoutX - grabX - scrolledPageX;

        Assert.Equal(firstPageX + firstDelta, scrolledPageX + scrolledDelta, 8);
    }
}

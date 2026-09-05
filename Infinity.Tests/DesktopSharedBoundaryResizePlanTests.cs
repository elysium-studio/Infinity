using Infinity.Platform.Abstractions;
using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopSharedBoundaryResizePlanTests
{
    [Fact]
    public void PointerDistanceIsConvertedFromOverlayScaleOnce()
    {
        DesktopSharedBoundaryResizePlan plan = Create();
        plan.Update(38, 0.38);
        Assert.Equal(100, plan.Delta);
        Assert.Equal(new(1000, 0, 600, 800), plan.GetPlacement(plan.Members[0]));
        Assert.Equal(new(1600, 0, 400, 800), plan.GetPlacement(plan.Members[1]));
    }

    [Fact]
    public void PreviewDoesNotMutateOriginalPlacements()
    {
        DesktopSharedBoundaryResizePlan plan = Create();
        DesktopSharedBoundaryMember[] originals = [.. plan.Members];
        plan.Update(38, 0.38);
        plan.Update(-19, 0.38);
        Assert.Equal(originals, plan.Members);
        Assert.Equal(new(1000, 0, 450, 800), plan.GetPlacement(plan.Members[0]));
    }

    [Fact]
    public void ReturningPointerToStartProducesNoResize()
    {
        DesktopSharedBoundaryResizePlan plan = Create();
        plan.Update(38, 0.38);
        plan.Update(0, 0.38);
        Assert.Equal(0, plan.Delta);
        Assert.All(plan.Members, member => Assert.Equal(member.Bounds, plan.GetPlacement(member)));
    }

    [Fact]
    public void SmallestAllowedWindowLimitsTheSharedPreview()
    {
        DesktopSharedBoundaryResizePlan plan = new([
            (new(1, new(0, 0, 500, 800), DesktopResizeEdge.Right), new(100, 100, 2000, 2000)),
            (new(2, new(500, 0, 500, 800), DesktopResizeEdge.Left), new(350, 100, 2000, 2000))]);
        plan.Update(380, 0.38);
        Assert.Equal(150, plan.Delta);
        Assert.Equal(350, plan.GetPlacement(plan.Members[1]).Width);
    }

    [Fact]
    public void HorizontalBoundaryPreservesPagePositionAndOutsideEdges()
    {
        DesktopSharedBoundaryResizePlan plan = new([
            (new(1, new(2000, 40, 1000, 400), DesktopResizeEdge.Bottom), new(100, 100, 2000, 2000)),
            (new(2, new(2000, 440, 1000, 400), DesktopResizeEdge.Top), new(100, 100, 2000, 2000))]);
        plan.Update(38, 0.38);
        Assert.Equal(new(2000, 40, 1000, 500), plan.GetPlacement(plan.Members[0]));
        Assert.Equal(new(2000, 540, 1000, 300), plan.GetPlacement(plan.Members[1]));
    }

    [Theory]
    [InlineData(double.NaN, 0.38)]
    [InlineData(double.PositiveInfinity, 0.38)]
    [InlineData(38, 0)]
    [InlineData(38, double.NaN)]
    public void InvalidPointerGeometryIsIgnored(double distance, double scale)
    {
        DesktopSharedBoundaryResizePlan plan = Create();
        plan.Update(distance, scale);
        Assert.Equal(0, plan.Delta);
    }

    private static DesktopSharedBoundaryResizePlan Create()
    {
        WindowResizeLimits limits = new(100, 100, 2000, 2000);
        return new([
            (new(1, new(1000, 0, 500, 800), DesktopResizeEdge.Right), limits),
            (new(2, new(1500, 0, 500, 800), DesktopResizeEdge.Left), limits)]);
    }
}

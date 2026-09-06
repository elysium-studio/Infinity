using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopSharedBoundaryCalculatorTests
{
    [Fact]
    public void OverlappingCandidatesDoNotProduceAmbiguousGroups()
    {
        IReadOnlyList<DesktopSharedBoundaryMember> group = DesktopSharedBoundaryCalculator.FindGroup(1, DesktopResizeEdge.Right,
            [(1, new(0, 0, 500, 800)), (2, new(500, 0, 500, 800)), (3, new(500, 0, 500, 800))]);
        Assert.Empty(group);
    }

    [Fact]
    public void DisconnectedAndCornerTouchingWindowsAreNotLinked()
    {
        IReadOnlyList<DesktopSharedBoundaryMember> group = DesktopSharedBoundaryCalculator.FindGroup(1, DesktopResizeEdge.Right,
            [(1, new(0, 0, 500, 400)), (2, new(500, 400, 500, 400)), (3, new(500, 500, 500, 400))]);
        Assert.Empty(group);
    }
}

using Infinity.Application.Abstractions;
using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopSnapSlotOccupancyResolverTests
{
    private readonly DesktopSnapSlotOccupancyResolver resolver = new(new(new TestWindowFrameGeometryReader()));
    private readonly DesktopSnapPlacement placement = new(100, 50, 800, 600);

    [Fact]
    public void MatchingWindowOccupiesSlot()
    {
        TrackedWindow occupant = CreateWindow(1, 100, 50, 800, 600);
        Assert.True(resolver.IsOccupied(placement, 2, [occupant]));
    }


    [Fact]
    public void DraggedWindowDoesNotOccupyItsOwnSlot()
    {
        TrackedWindow draggedWindow = CreateWindow(1, 100, 50, 800, 600);
        Assert.False(resolver.IsOccupied(placement, draggedWindow.Handle, [draggedWindow]));
    }


    [Fact]
    public void UnsnappedOverlappingWindowDoesNotReserveSlot()
    {
        TrackedWindow overlappingWindow = CreateWindow(1, 120, 70, 760, 560);
        Assert.False(resolver.IsOccupied(placement, 2, [overlappingWindow]));
    }


    [Fact]
    public void RoundedSnapGeometryStillOccupiesSlot()
    {
        TrackedWindow occupant = CreateWindow(1, 101, 49, 799, 601);
        Assert.True(resolver.IsOccupied(placement, 2, [occupant]));
    }


    [Fact]
    public void ReturnsFirstMatchingWindowInInputOrder()
    {
        TrackedWindow first = CreateWindow(3, 100, 50, 800, 600);
        TrackedWindow second = CreateWindow(2, 100, 50, 800, 600);
        Assert.True(resolver.TryGetOccupant(placement, 1, [first, second], out TrackedWindow? result));
        Assert.Same(first, result);
    }


    [Fact]
    public void GroupSelectionIsExcludedWithoutCopyingOrChangingTheSet()
    {
        DesktopWindowSelectionModel selection = new();
        selection.ToggleSelected(1);
        selection.ToggleSelected(2);
        TrackedWindow first = CreateWindow(1, 100, 50, 800, 600);
        TrackedWindow second = CreateWindow(2, 100, 50, 800, 600);
        TrackedWindow third = CreateWindow(3, 100, 50, 800, 600);
        Assert.True(resolver.TryGetOccupant(placement, selection.SelectedHandles, [first, second, third], out TrackedWindow? result));
        Assert.Same(third, result);
        Assert.Equal(2, selection.SelectedHandles.Count);
        selection.ToggleSelected(3);
        Assert.False(resolver.TryGetOccupant(placement, selection.SelectedHandles, [first, second, third], out result));
        Assert.Null(result);
    }


    private static TrackedWindow CreateWindow(nint handle, int x, int y, int width, int height) => new()
    {
        Handle = handle,
        CanvasX = x,
        CanvasY = y,
        Width = width,
        Height = height
    };
}

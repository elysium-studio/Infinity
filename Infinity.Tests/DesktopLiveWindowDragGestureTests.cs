using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopLiveWindowDragGestureTests
{
    [Fact]
    public void ModifiersHeldBeforeMovingCanOpenOverview()
    {
        DesktopLiveWindowDragGesture gesture = new();
        gesture.Begin(1, new(100, 100, 800, 600));
        Assert.False(gesture.CanOpen(true, true, false));
        gesture.Update(1, new(110, 120, 800, 600));
        Assert.True(gesture.CanOpen(true, true, false));
    }

    [Fact]
    public void ModifiersPressedAfterMovingCanOpenOverview()
    {
        DesktopLiveWindowDragGesture gesture = new();
        gesture.Begin(1, new(100, 100, 800, 600));
        gesture.Update(1, new(110, 120, 800, 600));
        Assert.False(gesture.CanOpen(false, true, false));
        Assert.True(gesture.CanOpen(true, true, false));
    }

    [Theory]
    [InlineData(90, 100, 810, 600)]
    [InlineData(100, 90, 800, 610)]
    [InlineData(100, 100, 810, 600)]
    [InlineData(100, 100, 800, 610)]
    [InlineData(90, 90, 810, 610)]
    public void ResizingDoesNotOpenOverview(int x, int y, int width, int height)
    {
        DesktopLiveWindowDragGesture gesture = new();
        gesture.Begin(1, new(100, 100, 800, 600));
        gesture.Update(1, new(x, y, width, height));
        Assert.False(gesture.CanOpen(true, true, false));
    }

    [Fact]
    public void DraggingAfterRestoringAMaximizedWindowCanOpenOverview()
    {
        DesktopLiveWindowDragGesture gesture = new();
        gesture.Begin(1, new(0, 0, 1920, 1080));
        gesture.Update(1, new(200, 100, 800, 600));
        Assert.False(gesture.CanOpen(true, true, false));
        gesture.Update(1, new(210, 110, 800, 600));
        Assert.True(gesture.CanOpen(true, true, false));
    }

    [Fact]
    public void LocationChangesOutsideANativeDragAreIgnored()
    {
        DesktopLiveWindowDragGesture gesture = new();
        gesture.Update(1, new(110, 120, 800, 600));
        Assert.False(gesture.CanOpen(true, true, false));
        gesture.Begin(1, new(100, 100, 800, 600));
        gesture.Update(2, new(110, 120, 800, 600));
        Assert.False(gesture.CanOpen(true, true, false));
    }

    [Fact]
    public void ReleasedButtonOrExistingOverlayPreventsOpening()
    {
        DesktopLiveWindowDragGesture gesture = new();
        gesture.Begin(1, new(100, 100, 800, 600));
        gesture.Update(1, new(110, 120, 800, 600));
        Assert.False(gesture.CanOpen(true, false, false));
        Assert.False(gesture.CanOpen(true, true, true));
    }

    [Fact]
    public void EndingDragClearsTheCandidate()
    {
        DesktopLiveWindowDragGesture gesture = new();
        gesture.Begin(1, new(100, 100, 800, 600));
        gesture.Update(1, new(110, 120, 800, 600));
        gesture.Reset();
        Assert.Equal(0, gesture.Window);
        Assert.False(gesture.CanOpen(true, true, false));
    }

    [Fact]
    public void BeginningAnotherDragClearsPreviouslyDetectedMovement()
    {
        DesktopLiveWindowDragGesture gesture = new();
        gesture.Begin(1, new(100, 100, 800, 600));
        gesture.Update(1, new(110, 120, 800, 600));
        gesture.Begin(2, new(100, 100, 800, 600));
        Assert.Equal(2, gesture.Window);
        Assert.False(gesture.CanOpen(true, true, false));
    }
}

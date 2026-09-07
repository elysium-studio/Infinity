using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopWindowPeekGestureTests
{
    [Fact]
    public void NoActiveSearchDoesNotStartPeek()
    {
        DesktopWindowPeekGesture gesture = new();
        Assert.False(gesture.Begin(42, 100, 200, true, true));
        Assert.False(gesture.IsActive(42));
    }

    [Fact]
    public void ClearingSearchBeforeReleaseDoesNotOpenPeek()
    {
        DesktopWindowPeekGesture gesture = new() { IsEnabled = true };
        Assert.True(gesture.Begin(42, 100, 200, true, true));
        gesture.IsEnabled = false;
        Assert.False(gesture.Complete(42, 100, 200));
    }

    [Fact]
    public void AltPrimaryClickCompletesOnceOnRelease()
    {
        DesktopWindowPeekGesture gesture = new() { IsEnabled = true };
        Assert.True(gesture.Begin(42, 100, 200, true, true));
        Assert.True(gesture.IsActive(42));
        Assert.True(gesture.Complete(42, 102, 201));
        Assert.False(gesture.IsActive(42));
        Assert.False(gesture.Complete(42, 102, 201));
    }

    [Fact]
    public void AnotherPointerCannotCompleteTheClick()
    {
        DesktopWindowPeekGesture gesture = new() { IsEnabled = true };
        Assert.True(gesture.Begin(42, 100, 200, true, true));
        Assert.False(gesture.Begin(99, 100, 200, true, true));
        Assert.False(gesture.Complete(99, 100, 200));
        Assert.True(gesture.Complete(42, 100, 200));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void OrdinaryClicksAndOtherButtonsDoNotStartPeek(bool primary, bool altOnly)
    {
        DesktopWindowPeekGesture gesture = new() { IsEnabled = true };
        Assert.False(gesture.Begin(42, 100, 200, primary, altOnly));
        Assert.False(gesture.IsActive(42));
    }

    [Fact]
    public void MovingAwayAndBackDoesNotBecomeAClick()
    {
        DesktopWindowPeekGesture gesture = new() { IsEnabled = true };
        gesture.Begin(42, 100, 200, true, true);
        gesture.Move(42, 110, 200);
        Assert.False(gesture.Complete(42, 100, 200));
        Assert.False(gesture.IsActive(42));
    }

    [Fact]
    public void CancelledPointerCannotOpenPeek()
    {
        DesktopWindowPeekGesture gesture = new() { IsEnabled = true };
        gesture.Begin(42, 100, 200, true, true);
        gesture.Cancel();
        Assert.False(gesture.Complete(42, 100, 200));
        Assert.True(gesture.Begin(42, 100, 200, true, true));
        Assert.True(gesture.Complete(42, 100, 200));
    }
}

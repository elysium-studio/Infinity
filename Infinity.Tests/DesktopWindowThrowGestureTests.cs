using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopWindowThrowGestureTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void FastHorizontalReleaseThrowsInThatDirection(int direction)
    {
        DesktopWindowThrowGesture gesture = Swipe(direction * 120, 8, 80);
        Assert.Equal(direction, gesture.Release(TimeSpan.FromMilliseconds(80)));
        Assert.Equal(0, gesture.Release(TimeSpan.FromMilliseconds(80)));
    }

    [Theory]
    [InlineData(120, 0, 240)]
    [InlineData(30, 0, 24)]
    [InlineData(120, 110, 80)]
    [InlineData(0, 180, 80)]
    public void OrdinaryShortSlowOrVerticalDragsDoNotThrow(double x, double y, int duration)
    {
        Assert.Equal(0, Swipe(x, y, duration).Release(TimeSpan.FromMilliseconds(duration)));
    }

    [Fact]
    public void PausingBeforeReleaseCancelsTheFlick()
    {
        DesktopWindowThrowGesture gesture = Swipe(120, 0, 80);
        gesture.Update(120, 0, TimeSpan.FromMilliseconds(160));
        Assert.Equal(0, gesture.Release(TimeSpan.FromMilliseconds(160)));
    }

    [Fact]
    public void PullingBackDoesNotThrowInTheOldDirection()
    {
        DesktopWindowThrowGesture gesture = Swipe(180, 0, 80);
        gesture.Update(130, 0, TimeSpan.FromMilliseconds(100));
        Assert.Equal(0, gesture.Release(TimeSpan.FromMilliseconds(100)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    public void InputFrequencyDoesNotChangeTheGesture(int interval)
    {
        DesktopWindowThrowGesture gesture = new();
        for (int time = 0; time <= 96; time += interval)
        {
            gesture.Update(time * 1.5, 0, TimeSpan.FromMilliseconds(time));
        }

        Assert.Equal(1, gesture.Release(TimeSpan.FromMilliseconds(96)));
    }

    [Fact]
    public void OldMovementAndResetCannotTriggerALaterDrop()
    {
        DesktopWindowThrowGesture gesture = Swipe(120, 0, 80);
        gesture.Reset();
        gesture.Update(120, 0, TimeSpan.FromMilliseconds(90));
        Assert.Equal(0, gesture.Release(TimeSpan.FromMilliseconds(100)));
    }

    [Theory]
    [InlineData(0, -1, 12, false, 0)]
    [InlineData(0, 1, 12, true, 1)]
    [InlineData(5, -1, 12, true, 4)]
    [InlineData(5, 1, 12, true, 6)]
    [InlineData(11, 1, 12, false, 11)]
    [InlineData(5, 2, 12, false, 5)]
    [InlineData(int.MaxValue, 1, null, false, int.MaxValue)]
    public void ThrowsOnlyOnePageAndRespectsLimits(int source, int direction, int? limit, bool expected, int target)
    {
        Assert.Equal(expected, DesktopWindowThrowGesture.TryGetTargetPage(source, direction, limit, out int actual));
        Assert.Equal(target, actual);
    }

    private static DesktopWindowThrowGesture Swipe(double x, double y, int duration)
    {
        DesktopWindowThrowGesture gesture = new();
        for (int step = 0; step <= 4; step++)
        {
            gesture.Update(x * step / 4, y * step / 4, TimeSpan.FromMilliseconds(duration * step / 4));
        }

        return gesture;
    }
}

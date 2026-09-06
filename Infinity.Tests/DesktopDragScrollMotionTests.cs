using Infinity.Application.Abstractions;
using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopDragScrollMotionTests
{
    [Fact]
    public void FastCursorMovementScrollsFasterThanSlowMovement()
    {
        DesktopDragScrollMotion slow = Create(1550);
        DesktopDragScrollMotion fast = Create(1550);
        slow.Update(1552, 1600, TimeSpan.FromMilliseconds(16));
        fast.Update(1570, 1600, TimeSpan.FromMilliseconds(16));
        Assert.True(Step(fast, 32) > Step(slow, 32) * 2);
    }

    [Fact]
    public void SlowingTheCursorReducesScrollingSpeed()
    {
        DesktopDragScrollMotion motion = Create(1540);
        motion.Update(1560, 1600, TimeSpan.FromMilliseconds(16));
        double fast = Step(motion, 32);
        for (int time = 48; time <= 160; time += 16)
        {
            motion.Update(1560 + (time - 32) / 160d, 1600, TimeSpan.FromMilliseconds(time));
            Step(motion, time);
        }

        Assert.True(Step(motion, 176) < fast);
    }

    [Theory]
    [InlineData(1560, 1580)]
    [InlineData(40, 20)]
    public void HoldingAtTheEdgeContinuesScrollingWithoutMorePointerEvents(double start, double end)
    {
        DesktopDragScrollMotion motion = Create(start);
        motion.Update(end, 1600, TimeSpan.FromMilliseconds(16));
        Assert.NotEqual(0, Step(motion, 32));
        for (int time = 48; time <= 1200; time += 16)
        {
            Step(motion, time);
        }

        Assert.Equal(Math.Sign(end - start), Math.Sign(Step(motion, 1216)));
        Assert.True(motion.IsMoving);
    }

    [Fact]
    public void PullingInwardBrakesWithoutScrollingTheOtherWay()
    {
        DesktopDragScrollMotion motion = Create(1550);
        motion.Update(1570, 1600, TimeSpan.FromMilliseconds(16));
        for (int time = 32; time <= 80; time += 16)
        {
            Step(motion, time);
        }

        double before = Step(motion, 96);
        Assert.True(before > 0);
        motion.Update(1568, 1600, TimeSpan.FromMilliseconds(104));
        Assert.InRange(Step(motion, 112), 0, before);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1599)]
    public void ArrivingAtTheScreenBoundaryInOneEventDoesNotNeedAnotherMovement(double x)
    {
        DesktopDragScrollMotion motion = Create(x);
        Assert.NotEqual(0, Step(motion, 16));
        for (int time = 32; time <= 5000; time += 16)
        {
            Assert.NotEqual(0, Step(motion, time));
        }
    }

    [Fact]
    public void LeavingTheEdgeZoneStopsWithoutCoastingIntoAnotherPage()
    {
        DesktopDragScrollMotion motion = Create(1599);
        Assert.True(Step(motion, 16) > 0);
        motion.Update(1500, 1600, TimeSpan.FromMilliseconds(32));
        Assert.Equal(0, Step(motion, 48));
        Assert.False(motion.IsMoving);
    }

    [Fact]
    public void PullingBackWithinTheZoneReducesTheSustainedSpeed()
    {
        DesktopDragScrollMotion motion = Create(1599);
        for (int time = 16; time <= 1000; time += 16)
        {
            Step(motion, time);
        }

        double before = Step(motion, 1008);
        motion.Update(1540, 1600, TimeSpan.FromMilliseconds(1016));
        Assert.InRange(Step(motion, 1024), 0, before / 4);
    }

    [Fact]
    public void InteriorMovementDoesNotScroll()
    {
        DesktopDragScrollMotion motion = Create(800);
        motion.Update(1000, 1600, TimeSpan.FromMilliseconds(16));
        Assert.Equal(0, Step(motion, 32));
    }

    [Fact]
    public void LeftAndRightHaveMatchingResponses()
    {
        DesktopDragScrollMotion left = Create(50);
        DesktopDragScrollMotion right = Create(1550);
        left.Update(30, 1600, TimeSpan.FromMilliseconds(16));
        right.Update(1570, 1600, TimeSpan.FromMilliseconds(16));
        Assert.Equal(-Step(left, 32), Step(right, 32), 6);
    }

    [Fact]
    public void OverviewScaleDoesNotChangeVisibleScrollSpeed()
    {
        DesktopDragScrollMotion small = Create(1550);
        DesktopDragScrollMotion large = Create(1550);
        small.Update(1570, 1600, TimeSpan.FromMilliseconds(16));
        large.Update(1570, 1600, TimeSpan.FromMilliseconds(16));
        double first = small.Advance(TimeSpan.FromMilliseconds(32), DragScrollSpeed.Normal, 0.25);
        double second = large.Advance(TimeSpan.FromMilliseconds(32), DragScrollSpeed.Normal, 0.5);
        Assert.Equal(first * 0.25, second * 0.5, 6);
    }

    [Fact]
    public void DragSpeedSettingStillControlsSensitivity()
    {
        DesktopDragScrollMotion slow = Create(1550);
        DesktopDragScrollMotion fast = Create(1550);
        slow.Update(1555, 1600, TimeSpan.FromMilliseconds(16));
        fast.Update(1555, 1600, TimeSpan.FromMilliseconds(16));
        double first = slow.Advance(TimeSpan.FromMilliseconds(32), DragScrollSpeed.Slow, 1);
        double second = fast.Advance(TimeSpan.FromMilliseconds(32), DragScrollSpeed.Fast, 1);
        Assert.Equal(first * 4, second, 6);
    }

    [Fact]
    public void DelayedUiTickCannotCauseAHugeJump()
    {
        DesktopDragScrollMotion motion = Create(1550);
        motion.Update(1599, 1600, TimeSpan.FromMilliseconds(1));
        Assert.InRange(Step(motion, 5000), 0, 128);
    }

    [Fact]
    public void ResetClearsMomentumBeforeAnotherDrag()
    {
        DesktopDragScrollMotion motion = Create(1550);
        motion.Update(1570, 1600, TimeSpan.FromMilliseconds(16));
        Step(motion, 32);
        motion.Reset();
        Assert.Equal(0, Step(motion, 48));
        Assert.False(motion.IsMoving);
    }

    [Theory]
    [InlineData(60)]
    [InlineData(120)]
    [InlineData(144)]
    [InlineData(240)]
    public void SustainedScrollingIntegratesTheSameDistanceAtDifferentRefreshRates(int refreshRate)
    {
        DesktopDragScrollMotion motion = Create(1600);
        double distance = 0;
        for (int frame = 1; frame <= refreshRate; frame++)
        {
            distance += motion.Advance(TimeSpan.FromSeconds((double)frame / refreshRate), DragScrollSpeed.Normal, 1);
        }

        double expected = 1000 * (1 - 0.06 * (1 - Math.Exp(-1 / 0.06)));
        Assert.Equal(expected, distance, 5);
    }

    [Fact]
    public void SmallFrameMovementRetainsFractionalPixels()
    {
        DesktopDragScrollMotion motion = Create(1537);
        double distance = motion.Advance(TimeSpan.FromSeconds(1d / 144), DragScrollSpeed.Normal, 1);
        Assert.InRange(distance, 0.001, 0.1);
        Assert.NotEqual(Math.Round(distance), distance);
    }

    [Fact]
    public void ResumingAfterAnIdleInteriorDoesNotIncludeIdleTime()
    {
        DesktopDragScrollMotion resumed = Create(800);
        resumed.Update(1600, 1600, TimeSpan.FromSeconds(10));
        DesktopDragScrollMotion fresh = Create(1600);
        double resumedDistance = resumed.Advance(TimeSpan.FromSeconds(10.016), DragScrollSpeed.Normal, 1);
        Assert.Equal(Step(fresh, 16), resumedDistance, 6);
    }

    private static DesktopDragScrollMotion Create(double pointerX)
    {
        DesktopDragScrollMotion motion = new();
        motion.Update(pointerX, 1600, TimeSpan.Zero);
        return motion;
    }

    private static double Step(DesktopDragScrollMotion motion, double milliseconds) => motion.Advance(TimeSpan.FromMilliseconds(milliseconds), DragScrollSpeed.Normal, 1);
}

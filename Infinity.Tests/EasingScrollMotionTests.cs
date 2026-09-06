using Infinity.Application;

namespace Infinity.Tests;

public sealed class EasingScrollMotionTests
{
    [Theory]
    [InlineData(60)]
    [InlineData(120)]
    [InlineData(144)]
    [InlineData(240)]
    public void FrameDrivenWheelMotionPreservesItsCurveAndExactDestination(int refreshRate)
    {
        ManualScrollTimeProvider time = new();
        EasingScrollMotion motion = new(time);
        motion.AddDelta(1000);
        double distance = motion.Drain();
        TimeSpan previous = TimeSpan.Zero;
        for (int frame = 1; frame <= refreshRate && motion.IsActive; frame++)
        {
            TimeSpan now = TimeSpan.FromSeconds((double)frame / refreshRate);
            time.Advance(now - previous);
            previous = now;
            double delta = motion.Drain();
            Assert.True(delta >= 0);
            distance += delta;
            if (frame == refreshRate / 4)
            {
                Assert.InRange(distance, 995, 998);
            }
        }

        Assert.False(motion.IsActive);
        Assert.Equal(1000, distance, 6);
    }

    [Fact]
    public void RetargetingPreservesTheElapsedFrameAndVelocity()
    {
        ManualScrollTimeProvider time = new();
        EasingScrollMotion uninterrupted = new(time);
        EasingScrollMotion retargeted = new(time);
        uninterrupted.AddDelta(1000);
        retargeted.AddDelta(1000);
        Assert.Equal(uninterrupted.Drain(), retargeted.Drain());
        time.Advance(TimeSpan.FromMilliseconds(16));
        retargeted.AddDelta(1000);
        Assert.True(retargeted.Drain() > uninterrupted.Drain());
    }


    [Fact]
    public void RapidRetargetsConserveTheFullRequestedDistance()
    {
        ManualScrollTimeProvider time = new();
        EasingScrollMotion motion = new(time);
        double distance = 0;
        for (int frame = 0; frame < 20; frame++)
        {
            for (int notch = 0; notch < 8; notch++)
            {
                time.Advance(TimeSpan.FromMilliseconds(2));
                motion.AddDelta(1000);
            }

            double delta = motion.Drain();
            Assert.True(delta > 0);
            distance += delta;
        }

        for (int frame = 0; frame < 180 && motion.IsActive; frame++)
        {
            time.Advance(TimeSpan.FromMilliseconds(16));
            distance += motion.Drain();
        }

        Assert.False(motion.IsActive);
        Assert.Equal(160_000, distance, 6);
    }
}

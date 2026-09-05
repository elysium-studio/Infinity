using Infinity.Application;

namespace Infinity.Tests;

public sealed class EasingScrollMotionTests
{
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

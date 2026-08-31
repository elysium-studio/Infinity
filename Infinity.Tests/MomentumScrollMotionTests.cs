using Infinity.Application;

namespace Infinity.Tests;

public sealed class MomentumScrollMotionTests
{
    [Fact]
    public void DrainAppliesFrictionAndPreservesSubPixelRemainder()
    {
        MomentumScrollMotion motion = new();
        motion.AddVelocity(10);

        Assert.Equal(8, motion.Drain());
        Assert.Equal(8, motion.Drain());
        Assert.True(motion.IsActive);
    }

    [Fact]
    public void SubThresholdVelocityStopsWithoutProducingMotion()
    {
        MomentumScrollMotion motion = new();
        motion.AddVelocity(0.1);

        Assert.False(motion.IsActive);
        Assert.Equal(0, motion.Drain());
    }

    [Fact]
    public void ResetClearsVelocityAndRemainder()
    {
        MomentumScrollMotion motion = new();
        motion.AddVelocity(-15);
        _ = motion.Drain();

        motion.Reset();

        Assert.False(motion.IsActive);
        Assert.Equal(0, motion.Drain());
    }
}

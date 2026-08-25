using Infinity.Application;

namespace Infinity.Tests;

public sealed class PixelScrollMotionTests
{
    [Fact]
    public void DrainReturnsPendingPixelsAndClearsMotion()
    {
        PixelScrollMotion motion = new();

        motion.AddDelta(8.5);
        motion.AddDelta(-1.25);

        Assert.True(motion.IsActive);
        Assert.Equal(7.25, motion.Drain());
        Assert.False(motion.IsActive);
        Assert.Equal(0, motion.Drain());
    }

    [Fact]
    public void ResetDiscardsPendingPixels()
    {
        PixelScrollMotion motion = new();
        motion.AddDelta(20);

        motion.Reset();

        Assert.False(motion.IsActive);
        Assert.Equal(0, motion.Drain());
    }
}

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

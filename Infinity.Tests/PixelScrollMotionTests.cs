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

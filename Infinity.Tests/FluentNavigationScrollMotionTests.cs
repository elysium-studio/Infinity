using Infinity.Application;

namespace Infinity.Tests;

public sealed class FluentNavigationScrollMotionTests
{
    [Fact]
    public void MotionCompletesAtTheFluentExistingElementDuration()
    {
        ManualTimeProvider timeProvider = new();
        FluentNavigationScrollMotion motion = new(timeProvider);

        motion.AddDelta(1000);

        Assert.Equal(0, motion.Drain());

        timeProvider.Advance(TimeSpan.FromMilliseconds(125));
        double firstHalf = motion.Drain();

        Assert.InRange(firstHalf, 0.01, 999.99);
        Assert.True(motion.IsActive);

        timeProvider.Advance(TimeSpan.FromMilliseconds(125));
        double secondHalf = motion.Drain();

        Assert.Equal(1000, firstHalf + secondHalf, 6);
        Assert.False(motion.IsActive);
    }

    [Fact]
    public void ResetDiscardsPendingNavigation()
    {
        ManualTimeProvider timeProvider = new();
        FluentNavigationScrollMotion motion = new(timeProvider);

        motion.AddDelta(1000);
        timeProvider.Advance(TimeSpan.FromMilliseconds(100));
        _ = motion.Drain();

        motion.Reset();

        Assert.False(motion.IsActive);
        Assert.Equal(0, motion.Drain());
    }

    private sealed class ManualTimeProvider :
        TimeProvider
    {
        private long timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => timestamp;

        public void Advance(TimeSpan duration) => timestamp += duration.Ticks;
    }
}

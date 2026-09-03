namespace Infinity.Tests;

internal sealed class ManualScrollTimeProvider : TimeProvider
{
    private long timestamp;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => timestamp;

    public void Advance(TimeSpan duration) => timestamp += duration.Ticks;
}

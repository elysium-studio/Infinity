using Infinity.Platform.Abstractions;

namespace Infinity.Platform.Windows;

public sealed class ScrollInputSuppression : IScrollInputSuppression
{
    private int suppressionCount;

    public bool IsSuppressed => Volatile.Read(ref suppressionCount) > 0;

    public IDisposable Suppress()
    {
        Interlocked.Increment(ref suppressionCount);
        return new ScrollInputSuppressionLease(this);
    }

    internal void Release() => Interlocked.Decrement(ref suppressionCount);
}

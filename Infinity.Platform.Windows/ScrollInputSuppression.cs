using Infinity.Platform.Abstractions;

namespace Infinity.Platform.Windows;

public sealed class ScrollInputSuppression : IScrollInputSuppression
{
    private int suppressionCount;
    private int keyboardSuppressionCount;

    public bool IsSuppressed => IsWheelSuppressed || Volatile.Read(ref keyboardSuppressionCount) > 0;

    public bool IsWheelSuppressed => Volatile.Read(ref suppressionCount) > 0;

    public IDisposable SuppressKeyboard()
    {
        Interlocked.Increment(ref keyboardSuppressionCount);
        return new ScrollInputSuppressionLease(this, true);
    }

    public IDisposable Suppress()
    {
        Interlocked.Increment(ref suppressionCount);
        return new ScrollInputSuppressionLease(this);
    }


    internal void Release(bool keyboardOnly)
    {
        if (keyboardOnly)
        {
            Interlocked.Decrement(ref keyboardSuppressionCount);
        }
        else
        {
            Interlocked.Decrement(ref suppressionCount);
        }
    }
}

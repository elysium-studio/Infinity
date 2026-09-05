using System.Threading;

namespace Infinity.Platform.Windows;

public sealed class WindowCaptureFrameState
{
    private long generation = 1;
    private long presentedGeneration;

    public long Generation => Volatile.Read(ref generation);

    public bool HasCurrentFrame
    {
        get
        {
            long current = Generation;
            return Volatile.Read(ref presentedGeneration) == current && Generation == current;
        }
    }


    public void Invalidate() => Interlocked.Increment(ref generation);

    public bool IsCurrent(long candidate) => candidate == Generation;

    public bool TryMarkPresented(long candidate)
    {
        if (!IsCurrent(candidate))
        {
            return false;
        }

        long previous = Interlocked.Exchange(ref presentedGeneration, candidate);
        return previous != candidate && IsCurrent(candidate);
    }
}

namespace Infinity.Platform.Windows;

internal sealed class ScrollInputSuppressionLease(ScrollInputSuppression owner) : IDisposable
{
    private ScrollInputSuppression? owner = owner;

    public void Dispose()
    {
        Interlocked.Exchange(ref owner, null)?.Release();
        GC.SuppressFinalize(this);
    }
}

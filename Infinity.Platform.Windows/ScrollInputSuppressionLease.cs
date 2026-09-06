namespace Infinity.Platform.Windows;

internal sealed class ScrollInputSuppressionLease(ScrollInputSuppression owner, bool keyboardOnly = false) : IDisposable
{
    private ScrollInputSuppression? owner = owner;

    public void Dispose()
    {
        Interlocked.Exchange(ref owner, null)?.Release(keyboardOnly);
        GC.SuppressFinalize(this);
    }
}

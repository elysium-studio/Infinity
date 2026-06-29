using Infinity.Platform.Abstractions;

namespace Infinity.Platform.Windows;

public class WindowMoveGuard :
    IWindowMoveGuard
{
    private readonly Action endScope;
    private volatile int depth;

    public WindowMoveGuard()
    {
        endScope = End;
    }

    public bool IsSystemMove => depth > 0;

    public WindowMoveScope Begin()
    {
        Interlocked.Increment(ref depth);
        return new WindowMoveScope(endScope);
    }

    private void End() => Interlocked.Decrement(ref depth);
}
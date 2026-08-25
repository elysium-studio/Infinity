using Infinity.Application.Abstractions;

namespace Infinity.Application;

public sealed class ScrollPresentationSession :
    IScrollPresentationSession
{
    private int isActive;

    public bool IsActive => Volatile.Read(ref isActive) != 0;

    public void Begin() => Interlocked.Exchange(ref isActive, 1);

    public void End() => Interlocked.Exchange(ref isActive, 0);
}

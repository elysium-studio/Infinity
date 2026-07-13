namespace Infinity.Application.Abstractions;

public interface IWindowDragScroller
{
    event Action? DragStarted;

    event Action? DragMoved;

    event Action? DragScrolled;

    event Action? DragStopped;

    bool IsAutoScrolling { get; }

    void Start();

    void Stop();
}
namespace Infinity.Application.Abstractions;

public interface IThumbnailDragScroller :
    IDisposable
{
    event Action? Scrolled;

    bool IsScrolling { get; }

    bool Begin(IntPtr windowHandle);

    void Update(IntPtr windowHandle, double pointerX, double viewportWidth);

    void End(IntPtr windowHandle);
}

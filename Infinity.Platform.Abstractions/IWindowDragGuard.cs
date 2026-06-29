namespace Infinity.Platform.Abstractions;

public interface IWindowDragGuard
{
    event Action? HoldStarted;

    bool IsDragging(IntPtr windowHandle);

    bool IsAnyDragging { get; }

    IntPtr DraggingWindow { get; }

    void Start();

    void Stop();
}
namespace Infinity.Application.Abstractions;

public interface ITrackedWindowDragController
{
    IntPtr DraggingWindow { get; }

    bool Begin(IntPtr windowHandle);

    bool MoveTo(IntPtr windowHandle, double canvasX, double canvasY);

    bool MoveAndResize(IntPtr windowHandle, double canvasX, double canvasY, double width, double height);

    void End(IntPtr windowHandle);
}

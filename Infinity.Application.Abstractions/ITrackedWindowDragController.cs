namespace Infinity.Application.Abstractions;

public interface ITrackedWindowDragController
{
    IntPtr DraggingWindow { get; }

    bool Begin(IntPtr windowHandle);

    bool MoveTo(IntPtr windowHandle, double canvasX, double canvasY);

    void End(IntPtr windowHandle);
}

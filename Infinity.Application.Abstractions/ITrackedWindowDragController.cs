namespace Infinity.Application.Abstractions;

public interface ITrackedWindowDragController
{
    IntPtr DraggingWindow { get; }

    bool Begin(IntPtr windowHandle);

    bool Move(IntPtr windowHandle, double horizontalDelta, double verticalDelta);

    void End(IntPtr windowHandle);
}
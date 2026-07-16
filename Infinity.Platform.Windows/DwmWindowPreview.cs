using Infinity.Platform.Abstractions;

namespace Infinity.Platform.Windows;

public class DwmWindowPreview(IDwmWindowPreviewSurface surface,
    nint windowHandle,
    long id) :
    IWindowPreview
{
    private int isDisposed;

    public nint WindowHandle { get; } = windowHandle;

    internal long Id { get; } = id;

    public void SetPlacement(double x,
        double y,
        double width,
        double height,
        int zIndex,
        bool isVisible,
        bool isElevated)
    {
        if (Volatile.Read(ref isDisposed) == 0)
        {
            surface.Apply(this, x, y, width, height, zIndex, isVisible, isElevated);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref isDisposed, 1) != 0)
        {
            return;
        }

        surface.Remove(this);
        GC.SuppressFinalize(this);
    }

    internal void MarkDisposed()
    {
        Interlocked.Exchange(ref isDisposed, 1);
        GC.SuppressFinalize(this);
    }
}

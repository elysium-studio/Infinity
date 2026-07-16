using Infinity.Platform.Abstractions;

namespace Infinity.Platform.Windows;

public class DwmWindowPreview(IDwmWindowPreviewSurface surface,
    nint windowHandle,
    long id,
    nint visual) :
    IWindowPreview
{
    private int isDisposed;

    public nint WindowHandle { get; } = windowHandle;

    public nint Visual { get; } = visual;

    internal long Id { get; } = id;

    public void Update(double width, double height, bool isVisible)
    {
        if (Volatile.Read(ref isDisposed) == 0)
        {
            surface.Apply(this, width, height, isVisible);
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

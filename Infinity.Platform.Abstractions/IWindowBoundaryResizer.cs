namespace Infinity.Platform.Abstractions;

public interface IWindowBoundaryResizer
{
    bool TryGetLimits(nint handle, out WindowResizeLimits limits);

    bool TryResize(nint handle, int x, int y, int width, int height);
}

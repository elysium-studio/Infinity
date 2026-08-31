namespace Infinity.Platform.Abstractions;

public interface IWindowResizeSynchronizer
{
    bool TrySynchronize(nint windowHandle, int width, int height);
}

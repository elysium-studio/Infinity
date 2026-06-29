namespace Infinity.Platform.Abstractions;

public interface IWindowGeometryReader
{
    bool TryReadGeometry(IntPtr windowHandle, out int x, out int y, out int width, out int height);

    bool IsVisible(IntPtr windowHandle);

    bool IsMinimised(IntPtr windowHandle);
}
namespace Infinity.Platform.Abstractions;

public interface IWindowMover
{
    void BeginBatch(int count);

    void MoveTo(IntPtr windowHandle, int x, int y, int width, int height);

    void EndBatch();

    void Flush();
}
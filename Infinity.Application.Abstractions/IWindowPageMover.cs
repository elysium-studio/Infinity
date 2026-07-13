namespace Infinity.Application.Abstractions;

public interface IWindowPageMover
{
    bool MoveToPage(IntPtr windowHandle, int targetPage);

    bool TryGetPage(IntPtr windowHandle, out int page);
}

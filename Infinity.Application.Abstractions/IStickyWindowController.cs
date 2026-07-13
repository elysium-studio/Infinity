namespace Infinity.Application.Abstractions;

public interface IStickyWindowController
{
    bool IsSticky(IntPtr windowHandle);

    bool Pin(IntPtr windowHandle);

    bool Unpin(IntPtr windowHandle);
}

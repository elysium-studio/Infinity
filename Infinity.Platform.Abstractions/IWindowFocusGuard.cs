namespace Infinity.Platform.Abstractions;

public interface IWindowFocusGuard
{
    bool IsDirect(IntPtr windowHandle);
}
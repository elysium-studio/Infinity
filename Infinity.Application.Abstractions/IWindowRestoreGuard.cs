namespace Infinity.Application.Abstractions;

public interface IWindowRestoreGuard
{
    bool IsRestoring(IntPtr windowHandle);

    void MarkRestoring(IntPtr windowHandle);
}
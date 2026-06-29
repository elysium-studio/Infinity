namespace Infinity.Platform.Abstractions;

public interface IWindowOpacity
{
    void SetOpacity(IntPtr windowHandle, byte opacity);

    void ClearOpacity(IntPtr windowHandle);
}
namespace Infinity.Platform.Abstractions;

public interface IWindowCloser
{
    bool TryClose(nint windowHandle);
}

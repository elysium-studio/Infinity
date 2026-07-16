namespace Infinity.Platform.Abstractions;

public interface IWindowZOrderController
{
    IDisposable? ElevateTemporarily(nint windowHandle);
}

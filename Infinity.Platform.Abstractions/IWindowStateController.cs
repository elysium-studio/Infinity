namespace Infinity.Platform.Abstractions;

public interface IWindowStateController
{
    WindowCommandState GetState(nint windowHandle);

    bool TryMaximize(nint windowHandle);

    bool TryRestore(nint windowHandle);

    bool TryMinimize(nint windowHandle);
}

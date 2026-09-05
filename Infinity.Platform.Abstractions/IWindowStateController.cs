namespace Infinity.Platform.Abstractions;

public interface IWindowStateController
{
    WindowCommandState GetState(nint windowHandle);

    bool TryMaximize(nint windowHandle);

    bool TryRestore(nint windowHandle);

    bool TryRestoreForMove(nint windowHandle, out WindowRestoreBounds bounds);

    bool TryMinimize(nint windowHandle);
}

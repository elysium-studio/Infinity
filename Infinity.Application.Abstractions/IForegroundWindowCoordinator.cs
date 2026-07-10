namespace Infinity.Application.Abstractions;

public interface IForegroundWindowCoordinator
{
    void HandleForegroundWindowChanged(IntPtr handle);

    void HandleWindowMinimizeStarted(IntPtr handle);

    void HandleWindowMinimizeEnded(IntPtr handle);

    void NotifyWindowClosed(IntPtr handle);
}

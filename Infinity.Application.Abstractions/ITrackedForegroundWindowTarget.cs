namespace Infinity.Application.Abstractions;

public interface ITrackedForegroundWindowTarget
{
    void SetTrackedForegroundWindow(nint windowHandle);
}

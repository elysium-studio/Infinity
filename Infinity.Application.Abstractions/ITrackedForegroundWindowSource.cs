namespace Infinity.Application.Abstractions;

public interface ITrackedForegroundWindowSource
{
    IntPtr GetTrackedForegroundWindow();
}

namespace Infinity.Platform.Abstractions;

public interface IForegroundWindowTracker
{
    event EventHandler<nint>? ForegroundWindowChanged;

    void Start();

    void Stop();

    void NotifyForegroundWindowChanged(nint windowHandle);
}

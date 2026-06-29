namespace Infinity.Application.Abstractions;

public interface IScrollInputSource
{
    event Action<int>? ScrollDeltaReceived;

    event Action<double>? ScrollVelocityIdle;

    event Action? MiddleButtonClicked;

    void Start();

    void Stop();
}
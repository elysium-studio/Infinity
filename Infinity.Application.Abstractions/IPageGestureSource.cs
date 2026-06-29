namespace Infinity.Application.Abstractions;

public interface IPageGestureSource
{
    event Action? SessionStarted;

    event Action? SessionEnded;

    void Start();

    void Stop();
}
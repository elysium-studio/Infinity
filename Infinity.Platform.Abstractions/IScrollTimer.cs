namespace Infinity.Platform.Abstractions;

public interface IScrollTimer
{
    event EventHandler Tick;

    void Start();

    void Stop();
}

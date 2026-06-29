namespace Infinity.Application.Abstractions;

public interface IScroller :
    IDisposable
{
    event EventHandler? ScrollStarted;

    event EventHandler? ScrollStopped;

    double VisualOffset { get; }

    void OnTick();

    void Reset();

    void ScrollBy(double delta);

    void ScrollTo(double offset, bool animate = true);

    void Start();

    void Stop();
}
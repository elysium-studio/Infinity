namespace Infinity.Application.Abstractions;

public interface IScroller : IDisposable
{
    event EventHandler? ScrollStarted;

    event EventHandler? ScrollStopped;

    double VisualOffset { get; }


    void CancelNavigation();

    void CommitPresentation();

    void OnTick();

    void Reposition();

    void Reset();


    void ScrollTo(double offset, bool animate = true);

    void Start();

    void Stop();
}

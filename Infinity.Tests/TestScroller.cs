using Infinity.Application.Abstractions;

namespace Infinity.Tests;

internal sealed class TestScroller : IScroller
{
    public event EventHandler? ScrollStarted;

    public event EventHandler? ScrollStopped;

    public double VisualOffset { get; set; }

    public int RepositionCount { get; private set; }

    public int ResetCount { get; private set; }


    public void CancelNavigation()
    {
    }


    public void Dispose() => GC.SuppressFinalize(this);

    public void CommitPresentation()
    {
    }


    public void OnTick()
    {
    }


    public void Reposition() => RepositionCount++;

    public void Reset() => ResetCount++;


    public void ScrollTo(double offset, bool animate = true) => VisualOffset = offset;

    public void Start() => ScrollStarted?.Invoke(this, EventArgs.Empty);

    public void Stop() => ScrollStopped?.Invoke(this, EventArgs.Empty);
}

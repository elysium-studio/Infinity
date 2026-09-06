using Infinity.Application;
using Infinity.Platform.Abstractions;
using Infinity.Shell;

namespace Infinity.Tests;

public sealed class PresentationScrollTimerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StartUsesOnlyTheTimerForTheCurrentPresentation(bool isPresentation)
    {
        ScrollPresentationSession session = new();
        if (isPresentation)
        {
            session.Begin();
        }

        TestTimer desktop = new();
        TestTimer rendering = new();
        using PresentationScrollTimer timer = new(desktop, rendering, session);
        int ticks = 0;
        timer.Tick += (_, _) => ticks++;
        timer.Start();
        timer.Start();
        desktop.Pulse();
        rendering.Pulse();
        Assert.Equal(1, ticks);
        Assert.Equal(isPresentation ? 0 : 1, desktop.Starts);
        Assert.Equal(isPresentation ? 1 : 0, rendering.Starts);
    }

    [Fact]
    public void OpeningTheOverlayHandsOffWithoutDeliveringTheOldTick()
    {
        ScrollPresentationSession session = new();
        TestTimer desktop = new();
        TestTimer rendering = new();
        using PresentationScrollTimer timer = new(desktop, rendering, session);
        int ticks = 0;
        timer.Tick += (_, _) => ticks++;
        timer.Start();
        session.Begin();
        desktop.Pulse();
        Assert.Equal(0, ticks);
        Assert.Equal(1, desktop.Stops);
        Assert.Equal(1, rendering.Starts);
        desktop.Pulse();
        rendering.Pulse();
        Assert.Equal(1, ticks);
    }

    [Fact]
    public void ClosingTheOverlayReturnsToDesktopTiming()
    {
        ScrollPresentationSession session = new();
        session.Begin();
        TestTimer desktop = new();
        TestTimer rendering = new();
        using PresentationScrollTimer timer = new(desktop, rendering, session);
        int ticks = 0;
        timer.Tick += (_, _) => ticks++;
        timer.Start();
        session.End();
        rendering.Pulse();
        Assert.Equal(0, ticks);
        Assert.Equal(1, rendering.Stops);
        Assert.Equal(1, desktop.Starts);
        rendering.Pulse();
        desktop.Pulse();
        Assert.Equal(1, ticks);
    }

    [Fact]
    public void StartAfterAChangeSwitchesImmediatelyWithoutWaitingForAnOldTick()
    {
        ScrollPresentationSession session = new();
        TestTimer desktop = new();
        TestTimer rendering = new();
        using PresentationScrollTimer timer = new(desktop, rendering, session);
        timer.Start();
        session.Begin();
        timer.Start();
        Assert.Equal(1, desktop.Stops);
        Assert.Equal(1, rendering.Starts);
    }

    [Fact]
    public void StopInsideATickIgnoresLateCallbacksUntilRestarted()
    {
        ScrollPresentationSession session = new();
        session.Begin();
        TestTimer desktop = new();
        TestTimer rendering = new();
        using PresentationScrollTimer timer = new(desktop, rendering, session);
        int ticks = 0;
        timer.Tick += (_, _) =>
        {
            ticks++;
            timer.Stop();
        };
        timer.Start();
        rendering.Pulse();
        rendering.Pulse();
        desktop.Pulse();
        Assert.Equal(1, ticks);
        timer.Start();
        rendering.Pulse();
        Assert.Equal(2, ticks);
    }

    [Fact]
    public void DisposeStopsCallbacksAndRejectsRestart()
    {
        ScrollPresentationSession session = new();
        TestTimer desktop = new();
        TestTimer rendering = new();
        PresentationScrollTimer timer = new(desktop, rendering, session);
        int ticks = 0;
        timer.Tick += (_, _) => ticks++;
        timer.Start();
        timer.Dispose();
        timer.Dispose();
        desktop.Pulse();
        rendering.Pulse();
        Assert.Equal(0, ticks);
        Assert.Equal(1, desktop.Stops);
        Assert.Throws<ObjectDisposedException>(timer.Start);
    }

    private sealed class TestTimer : IScrollTimer
    {
        public event EventHandler? Tick;

        public int Starts { get; private set; }

        public int Stops { get; private set; }

        public void Start() => Starts++;

        public void Stop() => Stops++;

        public void Pulse() => Tick?.Invoke(this, EventArgs.Empty);
    }
}

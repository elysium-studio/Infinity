using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Infinity.Application;
using Infinity.Application.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;

namespace Infinity.Tests;

public sealed class ThumbnailDragScrollerTests
{
    [Fact]
    public async Task EdgePositionStartsWhenModifierBecomesActiveAsync()
    {
        TestModifierKeyState modifier = new();
        PanState state = new();
        TestScroller scroller = new(state);
        using ThumbnailDragScroller dragScroller = CreateScroller(modifier, scroller, state);
        IntPtr handle = new(1);

        Assert.True(dragScroller.Begin(handle));
        dragScroller.Update(handle, 100, 100);
        Assert.False(dragScroller.IsScrolling);

        modifier.SetActive(true);

        double offset = await scroller.WaitForOffsetAsync(value => value > 0);
        Assert.True(offset > 0);
        Assert.True(dragScroller.IsScrolling);
    }

    [Fact]
    public async Task MovingInsideBoundaryStopsScrollingAsync()
    {
        TestModifierKeyState modifier = new() { IsActive = true };
        PanState state = new();
        TestScroller scroller = new(state);
        using ThumbnailDragScroller dragScroller = CreateScroller(modifier, scroller, state);
        IntPtr handle = new(1);

        Assert.True(dragScroller.Begin(handle));
        dragScroller.Update(handle, 100, 100);
        await scroller.WaitForOffsetAsync(value => value > 0);

        dragScroller.Update(handle, 50, 100);

        Assert.False(dragScroller.IsScrolling);
    }

    [Fact]
    public async Task MovingToOppositeEdgeReversesDirectionAsync()
    {
        TestModifierKeyState modifier = new() { IsActive = true };
        PanState state = new();
        state.SetOffset(1000);
        TestScroller scroller = new(state);
        using ThumbnailDragScroller dragScroller = CreateScroller(modifier, scroller, state);
        IntPtr handle = new(1);

        Assert.True(dragScroller.Begin(handle));
        dragScroller.Update(handle, 100, 100);
        double rightOffset = await scroller.WaitForOffsetAsync(value => value > 1000);

        dragScroller.Update(handle, 0, 100);

        double leftOffset = await scroller.WaitForOffsetAsync(value => value < rightOffset);
        Assert.True(leftOffset < rightOffset);
    }

    [Fact]
    public async Task ModifierReleaseStopsScrollingAsync()
    {
        TestModifierKeyState modifier = new() { IsActive = true };
        PanState state = new();
        TestScroller scroller = new(state);
        using ThumbnailDragScroller dragScroller = CreateScroller(modifier, scroller, state);
        IntPtr handle = new(1);

        Assert.True(dragScroller.Begin(handle));
        dragScroller.Update(handle, 100, 100);
        await scroller.WaitForOffsetAsync(value => value > 0);

        modifier.SetActive(false);

        Assert.False(dragScroller.IsScrolling);
    }

    [Fact]
    public async Task OnlyOwningDragCanUpdateOrEndSessionAsync()
    {
        TestModifierKeyState modifier = new() { IsActive = true };
        PanState state = new();
        TestScroller scroller = new(state);
        using ThumbnailDragScroller dragScroller = CreateScroller(modifier, scroller, state);
        IntPtr owner = new(1);
        IntPtr other = new(2);

        Assert.True(dragScroller.Begin(owner));
        Assert.False(dragScroller.Begin(other));

        dragScroller.Update(other, 100, 100);
        Assert.False(dragScroller.IsScrolling);

        dragScroller.Update(owner, 100, 100);
        await scroller.WaitForOffsetAsync(value => value > 0);
        dragScroller.End(other);
        Assert.True(dragScroller.IsScrolling);

        dragScroller.End(owner);
        Assert.False(dragScroller.IsScrolling);
    }

    private static ThumbnailDragScroller CreateScroller(IModifierKeyState modifier,
        IScroller scroller,
        IPanState state) =>
        new(modifier,
            scroller,
            state,
            new TestDispatcher(),
            () => new WindowDragScrollerConfiguration { SpeedLevel = DragScrollSpeed.Normal },
            NullLogger<ThumbnailDragScroller>.Instance);

    private sealed class TestModifierKeyState :
        IModifierKeyState
    {
        public event Action<bool>? StateChanged;

        public bool IsActive { get; set; }

        public void SetActive(bool value)
        {
            IsActive = value;
            StateChanged?.Invoke(value);
        }

        public void SetKeys(List<List<int>> combinations)
        {
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }

    private sealed class TestDispatcher :
        IDispatcher
    {
        public void Dispatch(Action action) => action();
    }

    private sealed class TestScroller(IPanState state) :
        IScroller
    {
        private readonly ConcurrentQueue<double> offsets = new();
        private readonly SemaphoreSlim offsetAvailable = new(0);

        public event EventHandler? ScrollStarted;

        public event EventHandler? ScrollStopped;

        public double VisualOffset => state.Offset;

        public void CommitPresentation()
        {
        }

        public void Dispose()
        {
            offsetAvailable.Dispose();
            GC.SuppressFinalize(this);
        }

        public void OnTick()
        {
        }

        public void Reposition()
        {
        }

        public void Reset()
        {
        }

        public void ScrollBy(double delta) => ScrollTo(state.Offset + delta, false);

        public void ScrollTo(double offset, bool animate = true)
        {
            state.SetOffset(offset);
            offsets.Enqueue(offset);
            offsetAvailable.Release();
        }

        public void Start() => ScrollStarted?.Invoke(this, EventArgs.Empty);

        public void Stop() => ScrollStopped?.Invoke(this, EventArgs.Empty);

        public async Task<double> WaitForOffsetAsync(Func<double, bool> predicate)
        {
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(1));

            while (true)
            {
                await offsetAvailable.WaitAsync(timeout.Token);

                if (offsets.TryDequeue(out double offset) && predicate(offset))
                {
                    return offset;
                }
            }
        }
    }
}

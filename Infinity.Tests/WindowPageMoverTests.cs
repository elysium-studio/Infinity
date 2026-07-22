using Infinity.Application;
using Infinity.Application.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Infinity.Tests;

public sealed class WindowPageMoverTests
{
    [Fact]
    public void MoveToPagePreservesPositionWithinPage()
    {
        WindowStore store = new();
        TrackedWindow window = CreateWindow(1250);
        store.Add(window);
        TestScroller scroller = new();
        TestPager pager = new();
        WindowPageMover mover = new(store, scroller, pager, () => 1000, NullLogger<WindowPageMover>.Instance);
        int changedCount = 0;
        store.WindowChanged += (_, _) => changedCount++;

        bool moved = mover.MoveToPage(window.Handle, 3);

        Assert.True(moved);
        Assert.Equal(3250, window.CanvasX);
        Assert.Equal(1, changedCount);
        Assert.Equal(1, scroller.RepositionCount);
        Assert.True(mover.TryGetPage(window.Handle, out int page));
        Assert.Equal(3, page);
    }

    [Fact]
    public void MoveToCurrentPageDoesNotRequestReposition()
    {
        WindowStore store = new();
        TrackedWindow window = CreateWindow(1250);
        store.Add(window);
        TestScroller scroller = new();
        WindowPageMover mover = new(store, scroller, new TestPager(), () => 1000,
            NullLogger<WindowPageMover>.Instance);

        bool moved = mover.MoveToPage(window.Handle, 1);

        Assert.True(moved);
        Assert.Equal(1250, window.CanvasX);
        Assert.Equal(0, scroller.RepositionCount);
    }

    [Fact]
    public void MoveToPageRejectsPagesOutsideFixedLimit()
    {
        WindowStore store = new();
        TrackedWindow window = CreateWindow(250);
        store.Add(window);
        TestScroller scroller = new();
        TestPager pager = new() { MaxPages = 3 };
        WindowPageMover mover = new(store, scroller, pager, () => 1000, NullLogger<WindowPageMover>.Instance);

        bool moved = mover.MoveToPage(window.Handle, 3);

        Assert.False(moved);
        Assert.Equal(250, window.CanvasX);
        Assert.Equal(0, scroller.RepositionCount);
    }

    [Fact]
    public void StickyWindowDoesNotBelongToOrMoveToASinglePage()
    {
        WindowStore store = new();
        TrackedWindow window = CreateWindow(1250);
        window.IsSticky = true;
        window.StickyViewportX = 250;
        store.Add(window);
        TestScroller scroller = new();
        WindowPageMover mover = new(store, scroller, new TestPager(), () => 1000,
            NullLogger<WindowPageMover>.Instance);

        bool hasPage = mover.TryGetPage(window.Handle, out _);
        bool moved = mover.MoveToPage(window.Handle, 3);

        Assert.False(hasPage);
        Assert.False(moved);
        Assert.Equal(1250, window.CanvasX);
        Assert.Equal(0, scroller.RepositionCount);
    }

    private static TrackedWindow CreateWindow(int canvasX) => new()
    {
        Handle = new IntPtr(1),
        CanvasX = canvasX,
        CanvasY = 100,
        Width = 800,
        Height = 600
    };

    private sealed class TestScroller : IScroller
    {
        public event EventHandler? ScrollStarted;

        public event EventHandler? ScrollStopped;

        public double VisualOffset => 0;

        public int RepositionCount { get; private set; }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        public void OnTick()
        {
        }

        public void Reposition() => RepositionCount++;

        public void Reset()
        {
        }

        public void ScrollBy(double delta)
        {
        }

        public void ScrollTo(double offset, bool animate = true)
        {
        }

        public void Start() => ScrollStarted?.Invoke(this, EventArgs.Empty);

        public void Stop() => ScrollStopped?.Invoke(this, EventArgs.Empty);
    }

    private sealed class TestPager : IPager
    {
        public event Action<int>? PageChanged;

        public int CurrentPage { get; private set; }

        public int PageCount => MaxPages ?? 1;

        public int? MaxPages { get; set; }

        public void NavigateToPage(int page)
        {
            CurrentPage = page;
            PageChanged?.Invoke(page);
        }

        public void SetMaxPages(int? maxPages) => MaxPages = maxPages;

        public void Start()
        {
        }

        public void Stop()
        {
        }
    }
}

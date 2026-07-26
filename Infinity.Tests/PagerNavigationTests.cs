using Elysium.Platform.Abstractions;
using Infinity.Application;
using Infinity.Application.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Infinity.Tests;

public sealed class PagerNavigationTests
{
    [Fact]
    public void NavigateToPageSuppressesForegroundFollowBeforeScrolling()
    {
        List<string> operations = [];
        TestForegroundWindowCoordinator foregroundCoordinator = new(operations);
        TestNavigationScroller scroller = new(operations);
        Pager pager = new(new WindowStore(),
            new PanState(),
            scroller,
            new TestWorkspace(),
            foregroundCoordinator,
            NullLogger<Pager>.Instance);

        pager.NavigateToPage(2);

        Assert.Equal(["SuppressForegroundFollow", "ScrollTo:2000"], operations);
    }

    private sealed class TestNavigationScroller(List<string> operations) :
        IScroller
    {
        public event EventHandler? ScrollStarted;

        public event EventHandler? ScrollStopped;

        public double VisualOffset { get; private set; }

        public void Dispose()
        {
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

        public void ScrollBy(double delta)
        {
        }

        public void ScrollTo(double offset, bool animate = true)
        {
            operations.Add($"ScrollTo:{offset}");
            VisualOffset = offset;
        }

        public void Start() => ScrollStarted?.Invoke(this, EventArgs.Empty);

        public void Stop() => ScrollStopped?.Invoke(this, EventArgs.Empty);
    }

    private sealed class TestWorkspace :
        IWorkspace
    {
        public event EventHandler? WorkspaceLayoutChanged;

        public int Height => 1000;

        public int Width => 1000;

        public int WorkAreaX => 0;

        public int WorkAreaY => 0;

        public IntPtr GetCurrentWorkspace()
        {
            WorkspaceLayoutChanged?.Invoke(this, EventArgs.Empty);
            return IntPtr.Zero;
        }
    }
}

internal sealed class TestForegroundWindowCoordinator(List<string>? operations = null) :
    IForegroundWindowCoordinator
{
    public void HandleForegroundWindowChanged(IntPtr handle)
    {
    }

    public void HandleWindowMinimizeStarted(IntPtr handle)
    {
    }

    public void HandleWindowMinimizeEnded(IntPtr handle)
    {
    }

    public void NotifyWindowClosed(IntPtr handle)
    {
    }

    public void SuppressForegroundFollow() => operations?.Add("SuppressForegroundFollow");
}

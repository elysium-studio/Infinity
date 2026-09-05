using Elysium.Platform.Abstractions;
using Infinity.Application;
using Infinity.Application.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Infinity.Tests;

public sealed class WindowPageJumperTests
{
    [Fact]
    public void MoveGestureUsesRememberedTrackedWindowWhileOverlayHasFocus()
    {
        WindowStore store = new();
        IntPtr applicationWindow = new(1);
        store.Add(new TrackedWindow { Handle = applicationWindow, CanvasX = 0, CanvasY = 0, Width = 1000, Height = 1000 });
        WindowArrowSwitchGesture arrowSwitch = new();
        WindowArrowMoveGesture arrowMove = new();
        WindowNumberSwitchGesture numberSwitch = new();
        WindowNumberMoveGesture numberMove = new();
        TestPager pager = new();
        WindowPageJumper jumper = new(arrowSwitch, arrowMove, numberSwitch, numberMove, new TestForegroundWindowSource(new IntPtr(99)), new TestTrackedForegroundWindowSource(applicationWindow), store, pager, new TestWorkspace(), NullLogger<WindowPageJumper>.Instance);
        jumper.Start();
        arrowMove.Invoke(0x27);
        Assert.True(store.TryGet(applicationWindow, out TrackedWindow movedWindow));
        Assert.Equal(1000, movedWindow.CanvasX);
        Assert.Equal(1, pager.CurrentPage);
    }


    private sealed class TestForegroundWindowSource(IntPtr handle) : IForegroundWindowSource
    {
        public IntPtr GetForegroundWindow() => handle;
    }


    private sealed class TestTrackedForegroundWindowSource(IntPtr handle) : ITrackedForegroundWindowSource
    {
        public IntPtr GetTrackedForegroundWindow() => handle;
    }


    private sealed class TestPager : IPager
    {
        public event Action<int>? PageChanged;

        public int CurrentPage { get; private set; }

        public int PageCount => CurrentPage + 1;

        public int? MaxPages => null;

        public bool IsPageCentered(int page) => CurrentPage == page;

        public void NavigateToPage(int page)
        {
            CurrentPage = page;
            PageChanged?.Invoke(page);
        }


        public void SetMaxPages(int? maxPages)
        {
        }


        public void Start()
        {
        }


        public void Stop()
        {
        }
    }


    private sealed class TestWorkspace : IWorkspace
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

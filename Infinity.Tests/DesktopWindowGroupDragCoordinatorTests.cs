using Elysium.Platform.Abstractions;
using Infinity.Application;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopWindowGroupDragCoordinatorTests
{
    private readonly WindowStore store = new();
    private readonly TestWorkspace workspace = new();
    private readonly TestPager pager = new();
    private readonly TestScroller scroller = new();

    [Fact]
    public void FreeDropPreservesRelativeFormationAcrossPages()
    {
        DesktopWindowGroupDragCoordinator coordinator = CreateCoordinator();
        TrackedWindow leader = AddWindow(1, 100, 100, 400, 300);
        TrackedWindow follower = AddWindow(2, 1200, 200, 400, 300);

        Assert.True(coordinator.Begin(leader.Handle, [leader.Handle, follower.Handle]));

        bool moved = coordinator.Complete(leader.Handle, 1064, 50, snapPlacement: null);

        Assert.True(moved);
        Assert.Equal((1100, 150), (leader.CanvasX, leader.CanvasY));
        Assert.Equal((2200, 250), (follower.CanvasX, follower.CanvasY));
        Assert.False(coordinator.IsActive);
        Assert.Equal(1, scroller.RepositionCount);
    }

    [Fact]
    public void SnapDropMovesFollowersBySamePageDeltaWithoutResizingThem()
    {
        DesktopWindowGroupDragCoordinator coordinator = CreateCoordinator();
        TrackedWindow leader = AddWindow(1, 100, 100, 400, 300);
        TrackedWindow follower = AddWindow(2, 1200, 200, 300, 250);
        DesktopSnapPlacement target = new(1006, 6, 488, 788);

        Assert.True(coordinator.Begin(leader.Handle, [leader.Handle, follower.Handle]));

        bool moved = coordinator.Complete(leader.Handle, 0, 0, target);

        Assert.True(moved);
        Assert.Equal((1006, 6, 488, 788), (leader.CanvasX, leader.CanvasY, leader.Width, leader.Height));
        Assert.Equal((2200, 200, 300, 250), (follower.CanvasX, follower.CanvasY, follower.Width, follower.Height));
    }

    [Fact]
    public void DropBackOnLeaderSourceRestoresEntireGroup()
    {
        DesktopWindowGroupDragCoordinator coordinator = CreateCoordinator();
        TrackedWindow leader = AddWindow(1, 100, 100, 400, 300);
        TrackedWindow follower = AddWindow(2, 1200, 200, 300, 250);
        DesktopSnapPlacement source = new(leader.CanvasX, leader.CanvasY, leader.Width, leader.Height);

        Assert.True(coordinator.Begin(leader.Handle, [leader.Handle, follower.Handle]));

        bool moved = coordinator.Complete(leader.Handle, 0, 0, source);

        Assert.False(moved);
        Assert.Equal((100, 100), (leader.CanvasX, leader.CanvasY));
        Assert.Equal((1200, 200), (follower.CanvasX, follower.CanvasY));
    }

    [Fact]
    public void FollowerConflictRejectsEntireDrop()
    {
        DesktopWindowGroupDragCoordinator coordinator = CreateCoordinator();
        TrackedWindow leader = AddWindow(1, 100, 100, 400, 300);
        TrackedWindow follower = AddWindow(2, 1200, 200, 300, 250);
        _ = AddWindow(3, 2200, 200, 300, 250);
        DesktopSnapPlacement target = new(1100, 100, 400, 300);

        Assert.True(coordinator.Begin(leader.Handle, [leader.Handle, follower.Handle]));

        bool moved = coordinator.Complete(leader.Handle, 0, 0, target);

        Assert.False(moved);
        Assert.Equal((100, 100), (leader.CanvasX, leader.CanvasY));
        Assert.Equal((1200, 200), (follower.CanvasX, follower.CanvasY));
        Assert.Equal(0, scroller.RepositionCount);
    }

    [Fact]
    public void OccupiedLeaderTargetSwapsOccupantToLeaderSource()
    {
        DesktopWindowGroupDragCoordinator coordinator = CreateCoordinator();
        TrackedWindow leader = AddWindow(1, 100, 100, 400, 300);
        TrackedWindow follower = AddWindow(2, 500, 200, 300, 250);
        TrackedWindow occupant = AddWindow(3, 1100, 100, 400, 300);
        DesktopSnapPlacement target = new(occupant.CanvasX, occupant.CanvasY, occupant.Width, occupant.Height);

        Assert.True(coordinator.Begin(leader.Handle, [leader.Handle, follower.Handle]));

        bool moved = coordinator.Complete(leader.Handle, 0, 0, target);

        Assert.True(moved);
        Assert.Equal((1100, 100), (leader.CanvasX, leader.CanvasY));
        Assert.Equal((1500, 200), (follower.CanvasX, follower.CanvasY));
        Assert.Equal((100, 100), (occupant.CanvasX, occupant.CanvasY));
    }

    [Fact]
    public void PageDeltaThatWouldMoveFollowerBeforeFirstPageIsRejected()
    {
        DesktopWindowGroupDragCoordinator coordinator = CreateCoordinator();
        TrackedWindow leader = AddWindow(1, 2100, 100, 400, 300);
        TrackedWindow follower = AddWindow(2, 100, 200, 300, 250);
        DesktopSnapPlacement target = new(100, 100, 400, 300);

        Assert.True(coordinator.Begin(leader.Handle, [leader.Handle, follower.Handle]));

        bool moved = coordinator.Complete(leader.Handle, 0, 0, target);

        Assert.False(moved);
        Assert.Equal((2100, 100), (leader.CanvasX, leader.CanvasY));
        Assert.Equal((100, 200), (follower.CanvasX, follower.CanvasY));
    }

    private DesktopWindowGroupDragCoordinator CreateCoordinator()
    {
        DesktopPageLayoutCalculator layoutCalculator = new();
        DesktopWindowDragPositionResolver dragPositionResolver = new(store, workspace, layoutCalculator);
        DesktopSnapSlotOccupancyResolver occupancyResolver = new();
        DesktopSnapPlacementResolver snapPlacementResolver = new(workspace, new DesktopSnapLayoutCatalog());
        DesktopWindowPlacementCoordinator placementCoordinator = new(store, scroller, workspace, new TestWindowResizeSynchronizer(), new TestWindowCloser(), new TestWindowStateController(), new TestWindowPageTransitionGuard(), snapPlacementResolver, occupancyResolver);
        return new DesktopWindowGroupDragCoordinator(store, workspace, pager, dragPositionResolver, occupancyResolver, placementCoordinator);
    }

    private TrackedWindow AddWindow(nint handle, int x, int y, int width, int height)
    {
        TrackedWindow window = new()
        {
            Handle = handle,
            CanvasX = x,
            CanvasY = y,
            Width = width,
            Height = height
        };
        store.Add(window);
        return window;
    }

    private sealed class TestWorkspace : IWorkspace
    {
        public event EventHandler? WorkspaceLayoutChanged;

        public int Width => 1000;

        public int Height => 800;

        public int WorkAreaX => 0;

        public int WorkAreaY => 0;

        public nint GetCurrentWorkspace()
        {
            WorkspaceLayoutChanged?.Invoke(this, EventArgs.Empty);
            return 0;
        }
    }

    private sealed class TestPager : IPager
    {
        public event Action<int>? PageChanged;

        public int CurrentPage => 0;

        public int PageCount => 10;

        public int? MaxPages { get; private set; }

        public bool IsPageCentered(int page) => page == CurrentPage;

        public void SetMaxPages(int? maxPages) => MaxPages = maxPages;

        public void NavigateToPage(int page) => PageChanged?.Invoke(page);

        public void Start() { }

        public void Stop() { }
    }

    private sealed class TestScroller : IScroller
    {
        public event EventHandler? ScrollStarted;

        public event EventHandler? ScrollStopped;

        public double VisualOffset => 0;

        public int RepositionCount { get; private set; }

        public void Reposition() => RepositionCount++;

        public void CancelNavigation() { }

        public void CommitPresentation() { }

        public void Dispose() { }

        public void OnTick() { }

        public void Reset() { }

        public void ScrollBy(double delta) => ScrollStarted?.Invoke(this, EventArgs.Empty);

        public void ScrollTo(double offset, bool animate = true) => ScrollStopped?.Invoke(this, EventArgs.Empty);

        public void Start() { }

        public void Stop() { }
    }

    private sealed class TestWindowResizeSynchronizer : IWindowResizeSynchronizer
    {
        public bool TrySynchronize(nint windowHandle, int width, int height) => true;
    }

    private sealed class TestWindowCloser : IWindowCloser
    {
        public bool TryClose(nint windowHandle) => true;
    }

    private sealed class TestWindowStateController : IWindowStateController
    {
        public WindowCommandState GetState(nint windowHandle) => WindowCommandState.Unavailable;
        public bool TryMaximize(nint windowHandle) => true;
        public bool TryRestore(nint windowHandle) => true;
        public bool TryMinimize(nint windowHandle) => true;
    }
}

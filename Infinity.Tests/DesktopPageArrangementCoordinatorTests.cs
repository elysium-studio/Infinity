using Elysium.Platform.Abstractions;
using Infinity.Application;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopPageArrangementCoordinatorTests
{
    [Fact]
    public void ArrangePreservesOccupiedSlotsAndFillsEmptySlots()
    {
        WindowStore store = new();
        TestWorkspace workspace = new();
        TestScroller scroller = new();
        DesktopSnapLayoutCatalog catalog = new();
        DesktopSnapSlotOccupancyResolver occupancyResolver = new(new(new TestWindowFrameGeometryReader()));
        DesktopSnapPlacementResolver placementResolver = new(workspace, catalog);
        DesktopWindowPlacementCoordinator placementCoordinator = new(store, scroller, workspace, new TestWindowResizeSynchronizer(), new TestWindowCloser(), new TestWindowStateController(), new TestWindowPageTransitionGuard(), placementResolver, occupancyResolver, new(new TestWindowFrameGeometryReader()));
        DesktopPageArrangementCoordinator coordinator = new(store, catalog, placementResolver, occupancyResolver, placementCoordinator);
        TrackedWindow occupied = AddWindow(store, 1, 0, 0, 960, 1040);
        TrackedWindow floating = AddWindow(store, 2, 1200, 200, 500, 400);
        List<nint> animatedHandles = [];
        int completedBatches = 0;
        placementCoordinator.PlacementStarting += handles => animatedHandles.AddRange(handles);
        placementCoordinator.PlacementCompleted += _ => completedBatches++;
        int arranged = coordinator.Arrange(0, DesktopSnapLayoutKind.Halves, 0, 0);
        Assert.Equal(1, arranged);
        Assert.Equal((0, 0, 960, 1040), (occupied.CanvasX, occupied.CanvasY, occupied.Width, occupied.Height));
        Assert.Equal((960, 0, 960, 1040), (floating.CanvasX, floating.CanvasY, floating.Width, floating.Height));
        Assert.Equal(1, scroller.RepositionCount);
        Assert.Equal(new nint[] { 2 }, animatedHandles);
        Assert.Equal(1, completedBatches);
    }


    [Fact]
    public void ArrangeLeavesExcessWindowsFloating()
    {
        WindowStore store = new();
        TestWorkspace workspace = new();
        TestScroller scroller = new();
        DesktopSnapLayoutCatalog catalog = new();
        DesktopSnapSlotOccupancyResolver occupancyResolver = new(new(new TestWindowFrameGeometryReader()));
        DesktopSnapPlacementResolver placementResolver = new(workspace, catalog);
        DesktopWindowPlacementCoordinator placementCoordinator = new(store, scroller, workspace, new TestWindowResizeSynchronizer(), new TestWindowCloser(), new TestWindowStateController(), new TestWindowPageTransitionGuard(), placementResolver, occupancyResolver, new(new TestWindowFrameGeometryReader()));
        DesktopPageArrangementCoordinator coordinator = new(store, catalog, placementResolver, occupancyResolver, placementCoordinator);
        AddWindow(store, 1, 100, 100, 400, 300);
        AddWindow(store, 2, 600, 100, 400, 300);
        TrackedWindow excess = AddWindow(store, 3, 1100, 100, 400, 300);
        int arranged = coordinator.Arrange(0, DesktopSnapLayoutKind.Halves, 0, 0);
        Assert.Equal(2, arranged);
        Assert.Equal((1100, 100, 400, 300), (excess.CanvasX, excess.CanvasY, excess.Width, excess.Height));
    }


    private static TrackedWindow AddWindow(WindowStore store, nint handle, int x, int y, int width, int height)
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

        public int Width => 1920;

        public int Height => 1040;

        public int WorkAreaX => 0;

        public int WorkAreaY => 0;

        public nint GetCurrentWorkspace()
        {
            WorkspaceLayoutChanged?.Invoke(this, EventArgs.Empty);
            return 0;
        }
    }


    private sealed class TestScroller : IScroller
    {
        public event EventHandler? ScrollStarted;

        public event EventHandler? ScrollStopped;

        public double VisualOffset => 0;

        public int RepositionCount { get; private set; }


        public void Reposition() => RepositionCount++;

        public void CancelNavigation()
        {
        }


        public void CommitPresentation()
        {
        }


        public void Dispose()
        {
        }


        public void OnTick()
        {
        }


        public void Reset()
        {
        }


        public void ScrollBy(double delta) => ScrollStarted?.Invoke(this, EventArgs.Empty);

        public void ScrollTo(double offset, bool animate = true) => ScrollStopped?.Invoke(this, EventArgs.Empty);

        public void Start()
        {
        }


        public void Stop()
        {
        }
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

        public bool TryRestoreForMove(nint windowHandle, out WindowRestoreBounds bounds)
        {
            bounds = default;
            return false;
        }


        public bool TryMinimize(nint windowHandle) => true;
    }
}

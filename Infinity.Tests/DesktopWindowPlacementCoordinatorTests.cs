using Elysium.Platform.Abstractions;
using Infinity.Application;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopWindowPlacementCoordinatorTests
{
    private readonly WindowStore store = new();
    private readonly TestScroller scroller = new();
    private readonly TestWorkspace workspace = new();
    private readonly TestWindowResizeSynchronizer resizeSynchronizer = new();
    private readonly TestWindowCloser windowCloser = new();
    private readonly TestWindowStateController windowStateController = new();
    private readonly TestWindowPageTransitionGuard pageTransitionGuard = new();

    [Fact]
    public void SwapIntoSlotMovesOccupantToDraggedWindowsPreviousBounds()
    {
        DesktopWindowPlacementCoordinator coordinator = CreateCoordinator();
        TrackedWindow moving = AddWindow(1, 120, 80, 700, 500);
        TrackedWindow occupant = AddWindow(2, 1006, 6, 948, 1028);
        DesktopSnapPlacement target = new(1006, 6, 948, 1028);

        Assert.True(coordinator.TrySwapIntoSlot(moving.Handle, occupant.Handle, target));
        Assert.Equal((1006, 6, 948, 1028), (moving.CanvasX, moving.CanvasY, moving.Width, moving.Height));
        Assert.Equal((120, 80, 700, 500), (occupant.CanvasX, occupant.CanvasY, occupant.Width, occupant.Height));
        Assert.Equal(1, scroller.RepositionCount);
    }

    [Fact]
    public void MoveToOccupiedSlotSwapsInsteadOfOverlapping()
    {
        DesktopWindowPlacementCoordinator coordinator = CreateCoordinator();
        TrackedWindow moving = AddWindow(1, 100, 100, 600, 400);
        TrackedWindow occupant = AddWindow(2, 1006, 6, 948, 1028);

        Assert.True(coordinator.TryMoveToSlot(moving.Handle, 0, DesktopSnapLayoutKind.Halves, 1, 40, 0));
        Assert.Equal((1006, 6, 948, 1028), (moving.CanvasX, moving.CanvasY, moving.Width, moving.Height));
        Assert.Equal((100, 100, 600, 400), (occupant.CanvasX, occupant.CanvasY, occupant.Width, occupant.Height));
    }

    [Fact]
    public void MoveToPagePreservesRelativePositionWithinWorkArea()
    {
        DesktopWindowPlacementCoordinator coordinator = CreateCoordinator();
        TrackedWindow window = AddWindow(1, 300, 140, 800, 600);

        Assert.True(coordinator.TryMoveToPage(window.Handle, 2, center: false));
        Assert.Equal(4140, window.CanvasX);
        Assert.Equal(140, window.CanvasY);
    }

    [Fact]
    public void CloseDelegatesToPlatformWindowCloser()
    {
        DesktopWindowPlacementCoordinator coordinator = CreateCoordinator();

        Assert.True(coordinator.TryClose(42));
        Assert.Equal((nint)42, windowCloser.ClosedHandle);
    }

    [Fact]
    public void WindowStateCommandsDelegateToPlatformController()
    {
        DesktopWindowPlacementCoordinator coordinator = CreateCoordinator();
        AddWindow(42, 120, 80, 700, 500);

        Assert.Equal(windowStateController.State, coordinator.GetWindowCommandState(42));
        Assert.True(coordinator.TryMinimize(42));
        Assert.True(coordinator.TryMaximize(42));
        Assert.True(coordinator.TryRestore(42));
        Assert.Equal(((nint)42, "Minimize"), windowStateController.Commands[0]);
        Assert.Equal(((nint)42, "Maximize"), windowStateController.Commands[1]);
        Assert.Equal(((nint)42, "Restore"), windowStateController.Commands[2]);
        Assert.Equal((nint)42, pageTransitionGuard.WindowHandle);
        Assert.Equal(0, pageTransitionGuard.Page);
    }

    [Fact]
    public void MoveByPagesMovesSelectedWindowsAsOneBatch()
    {
        DesktopWindowPlacementCoordinator coordinator = CreateCoordinator();
        TrackedWindow first = AddWindow(1, 120, 80, 700, 500);
        TrackedWindow second = AddWindow(2, 900, 120, 600, 400);

        int moved = coordinator.MoveByPages([first.Handle, second.Handle], 1, maximumPageCount: null);

        Assert.Equal(2, moved);
        Assert.Equal(2040, first.CanvasX);
        Assert.Equal(2820, second.CanvasX);
        Assert.Equal(1, scroller.RepositionCount);
    }

    [Fact]
    public void MoveByPagesHonoursFixedPageBoundary()
    {
        DesktopWindowPlacementCoordinator coordinator = CreateCoordinator();
        TrackedWindow window = AddWindow(1, 120, 80, 700, 500);

        int moved = coordinator.MoveByPages([window.Handle], -1, maximumPageCount: 2);

        Assert.Equal(0, moved);
        Assert.Equal(120, window.CanvasX);
    }

    private DesktopWindowPlacementCoordinator CreateCoordinator()
    {
        DesktopSnapSlotOccupancyResolver occupancyResolver = new();
        DesktopSnapPlacementResolver placementResolver = new(workspace, new DesktopSnapLayoutCatalog());
        return new DesktopWindowPlacementCoordinator(store, scroller, workspace, resizeSynchronizer, windowCloser, windowStateController, pageTransitionGuard, placementResolver, occupancyResolver);
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

        public int Width => 1920;

        public int Height => 1040;

        public int WorkAreaX => 100;

        public int WorkAreaY => 40;

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
        public nint ClosedHandle { get; private set; }

        public bool TryClose(nint windowHandle)
        {
            ClosedHandle = windowHandle;
            return true;
        }
    }

    private sealed class TestWindowStateController : IWindowStateController
    {
        public WindowCommandState State { get; } = new(true, true, false);

        public List<(nint Handle, string Command)> Commands { get; } = [];

        public WindowCommandState GetState(nint windowHandle) => State;

        public bool TryMaximize(nint windowHandle)
        {
            Commands.Add((windowHandle, "Maximize"));
            return true;
        }

        public bool TryMinimize(nint windowHandle)
        {
            Commands.Add((windowHandle, "Minimize"));
            return true;
        }

        public bool TryRestore(nint windowHandle)
        {
            Commands.Add((windowHandle, "Restore"));
            return true;
        }

    }

    private sealed class TestWindowPageTransitionGuard :
        IWindowPageTransitionGuard
    {
        public nint WindowHandle { get; private set; }

        public int Page { get; private set; }

        public void PreservePage(nint windowHandle, int page, int workspaceWidth, int workAreaX)
        {
            WindowHandle = windowHandle;
            Page = page;
        }

        public bool TryMapToPreservedPage(nint windowHandle, int candidateCanvasX, int windowWidth, out int mappedCanvasX)
        {
            mappedCanvasX = candidateCanvasX;
            return false;
        }

        public void Clear(nint windowHandle)
        {
        }
    }
}

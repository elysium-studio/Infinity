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
    private readonly TestWindowFrameGeometryReader geometryReader = new();

    [Fact]
    public void SnappingDifferentFramesAlignsVisibleBottomsAndAdjoiningEdges()
    {
        geometryReader.Insets[1] = (8, 0, 8, 8);
        geometryReader.Insets[2] = (0, 0, 0, 0);
        DesktopWindowPlacementCoordinator coordinator = CreateCoordinator();
        TrackedWindow first = AddWindow(1, 200, 100, 700, 500);
        TrackedWindow second = AddWindow(2, 1100, 100, 700, 500);
        Assert.True(coordinator.TryMoveToSlot(1, 0, DesktopSnapLayoutKind.Halves, 0, 100, 40));
        Assert.True(coordinator.TryMoveToSlot(2, 0, DesktopSnapLayoutKind.Halves, 1, 100, 40));
        Assert.Equal((92, 40, 976, 1048), (first.CanvasX, first.CanvasY, first.Width, first.Height));
        Assert.Equal((1060, 40, 960, 1040), (second.CanvasX, second.CanvasY, second.Width, second.Height));
        DesktopWindowFrameGeometry frames = new(geometryReader);
        DesktopSnapPlacement left = frames.GetVisiblePlacement(first), right = frames.GetVisiblePlacement(second);
        Assert.Equal(left.CanvasX + left.Width, right.CanvasX);
        Assert.Equal(left.CanvasY + left.Height, right.CanvasY + right.Height);
        Assert.Equal(1080, right.CanvasY + right.Height);
    }


    [Fact]
    public void SwapUsesEachRecipientsOwnInvisibleBorders()
    {
        geometryReader.Insets[1] = (8, 0, 8, 8);
        geometryReader.Insets[2] = (0, 0, 0, 0);
        DesktopWindowPlacementCoordinator coordinator = CreateCoordinator();
        TrackedWindow first = AddWindow(1, 92, 40, 976, 1048);
        TrackedWindow second = AddWindow(2, 1060, 40, 960, 1040);
        Assert.True(coordinator.TryMoveToSlot(1, 0, DesktopSnapLayoutKind.Halves, 1, 100, 40));
        Assert.Equal((1052, 40, 976, 1048), (first.CanvasX, first.CanvasY, first.Width, first.Height));
        Assert.Equal((100, 40, 960, 1040), (second.CanvasX, second.CanvasY, second.Width, second.Height));
    }


    [Fact]
    public void SendingBetweenPagesDoesNotApplyFrameInsetsAgain()
    {
        geometryReader.Insets[1] = (8, 0, 8, 8);
        TrackedWindow window = AddWindow(1, 300, 140, 800, 600);
        Assert.True(CreateCoordinator().TryMoveToPage(1, 1, false));
        Assert.Equal((2220, 140, 800, 600), (window.CanvasX, window.CanvasY, window.Width, window.Height));
    }


    [Theory]
    [InlineData(92, 2012)]
    [InlineData(1052, 2972)]
    public void SendingSnappedWindowToAnotherPageKeepsEdgeAlignment(int sourceX, int destinationX)
    {
        geometryReader.Insets[1] = (8, 0, 8, 8);
        TrackedWindow window = AddWindow(1, sourceX, 40, 976, 1048);
        Assert.True(CreateCoordinator().TryMoveToPage(1, 1, false));
        Assert.Equal((destinationX, 40, 976, 1048), (window.CanvasX, window.CanvasY, window.Width, window.Height));
    }


    [Fact]
    public void SwapIntoSlotMovesOccupantToDraggedWindowsPreviousBounds()
    {
        DesktopWindowPlacementCoordinator coordinator = CreateCoordinator();
        TrackedWindow moving = AddWindow(1, 120, 80, 700, 500);
        TrackedWindow occupant = AddWindow(2, 1000, 0, 960, 1040);
        DesktopSnapPlacement target = new(1000, 0, 960, 1040);
        Assert.True(coordinator.TrySwapIntoSlot(moving.Handle, occupant.Handle, target));
        Assert.Equal((1000, 0, 960, 1040), (moving.CanvasX, moving.CanvasY, moving.Width, moving.Height));
        Assert.Equal((120, 80, 700, 500), (occupant.CanvasX, occupant.CanvasY, occupant.Width, occupant.Height));
        Assert.Equal(1, scroller.RepositionCount);
    }


    [Fact]
    public void SlotSwapBracketsBothWindowsWithOneAnimationBatch()
    {
        DesktopWindowPlacementCoordinator coordinator = CreateCoordinator();
        TrackedWindow moving = AddWindow(1, 120, 80, 700, 500);
        TrackedWindow occupant = AddWindow(2, 1000, 0, 960, 1040);
        List<string> events = [];
        coordinator.PlacementStarting += handles =>  {  Assert.Equal(new nint[] { 2, 1 }, handles);  Assert.Equal(120, moving.CanvasX);  Assert.Equal(1000, occupant.CanvasX);  events.Add("start");  };
        coordinator.PlacementCompleted += handles =>  {  Assert.Equal(new nint[] { 2, 1 }, handles);  Assert.Equal(1000, moving.CanvasX);  Assert.Equal(120, occupant.CanvasX);  events.Add("complete");  };
        Assert.True(coordinator.TrySwapIntoSlot(1, 2, new(1000, 0, 960, 1040)));
        Assert.Equal(new[] { "start", "complete" }, events);
    }


    [Fact]
    public void FailedAnimatedPlacementStillEndsTheBatch()
    {
        DesktopWindowPlacementCoordinator coordinator = CreateCoordinator();
        TrackedWindow window = AddWindow(1, 100, 40, 1920, 1040);
        windowStateController.State = new(true, false, true);
        windowStateController.RestoreSucceeds = false;
        List<string> events = [];
        coordinator.PlacementStarting += _ => events.Add("start");
        coordinator.PlacementCompleted += _ => events.Add("complete");
        Assert.False(coordinator.ApplyPlacements([(window, new(1000, 0, 960, 1040))], animate: true));
        Assert.Equal(new[] { "start", "complete" }, events);
    }


    [Fact]
    public void SendingToAnotherPageAnimatesFromSourceToDestination()
    {
        DesktopWindowPlacementCoordinator coordinator = CreateCoordinator();
        TrackedWindow window = AddWindow(1, 300, 140, 800, 600);
        List<string> events = [];
        coordinator.PlacementStarting += handles =>  {  Assert.Equal((nint)1, Assert.Single(handles));  Assert.Equal(300, window.CanvasX);  events.Add("start");  };
        coordinator.PlacementCompleted += handles =>  {  Assert.Equal((nint)1, Assert.Single(handles));  Assert.Equal(2220, window.CanvasX);  events.Add("complete");  };
        Assert.True(coordinator.TryMoveToPage(1, 1, false));
        Assert.Equal(new[] { "start", "complete" }, events);
    }


    [Fact]
    public void EmptyArrangementDoesNotStartAnAnimationBatch()
    {
        DesktopWindowPlacementCoordinator coordinator = CreateCoordinator();
        int starts = 0;
        coordinator.PlacementStarting += _ => starts++;
        Assert.True(coordinator.ApplyPlacements([], animate: true));
        Assert.Equal(0, starts);
    }


    [Fact]
    public void MoveToOccupiedSlotSwapsInsteadOfOverlapping()
    {
        DesktopWindowPlacementCoordinator coordinator = CreateCoordinator();
        TrackedWindow moving = AddWindow(1, 100, 100, 600, 400);
        TrackedWindow occupant = AddWindow(2, 1000, 0, 960, 1040);
        Assert.True(coordinator.TryMoveToSlot(moving.Handle, 0, DesktopSnapLayoutKind.Halves, 1, 40, 0));
        Assert.Equal((1000, 0, 960, 1040), (moving.CanvasX, moving.CanvasY, moving.Width, moving.Height));
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
    public void PreparingMaximisedWindowRestoresNormalBoundsOnItsExistingPage()
    {
        DesktopWindowPlacementCoordinator coordinator = CreateCoordinator();
        TrackedWindow window = AddWindow(1, 3940, 40, 1920, 1040);
        windowStateController.State = new(true, false, true);
        Assert.True(coordinator.TryPrepareForMove(1, out DesktopSnapPlacement restored, out DesktopSnapPlacement original));
        Assert.Equal(new DesktopSnapPlacement(3940, 40, 1920, 1040), original);
        Assert.Equal(new DesktopSnapPlacement(4140, 140, 800, 600), restored);
        Assert.Equal((4140, 140, 800, 600), (window.CanvasX, window.CanvasY, window.Width, window.Height));
        Assert.Equal(2, pageTransitionGuard.Page);
        Assert.Equal(((nint)1, "RestoreForMove"), Assert.Single(windowStateController.Commands));
    }


    [Fact]
    public void PreparingNormalWindowDoesNotRestoreOrRepositionIt()
    {
        DesktopWindowPlacementCoordinator coordinator = CreateCoordinator();
        AddWindow(1, 300, 140, 800, 600);
        Assert.True(coordinator.TryPrepareForMove(1, out DesktopSnapPlacement restored, out DesktopSnapPlacement original));
        Assert.Equal(original, restored);
        Assert.Empty(windowStateController.Commands);
        Assert.Equal(0, scroller.RepositionCount);
    }


    [Fact]
    public void SnappingMaximisedWindowRestoresThenAppliesSlotBounds()
    {
        DesktopWindowPlacementCoordinator coordinator = CreateCoordinator();
        TrackedWindow window = AddWindow(1, 100, 40, 1920, 1040);
        windowStateController.State = new(true, false, true);
        Assert.True(coordinator.TryMoveToSlot(1, 2, DesktopSnapLayoutKind.Halves, 1, 40, 0));
        Assert.Equal(((nint)1, "RestoreForMove"), Assert.Single(windowStateController.Commands));
        Assert.Equal((4840, 0, 960, 1040), (window.CanvasX, window.CanvasY, window.Width, window.Height));
        Assert.Equal(2, pageTransitionGuard.Page);
    }


    [Fact]
    public void FailedRestoreDoesNotApplySnapBounds()
    {
        DesktopWindowPlacementCoordinator coordinator = CreateCoordinator();
        TrackedWindow window = AddWindow(1, 100, 40, 1920, 1040);
        windowStateController.State = new(true, false, true);
        windowStateController.RestoreSucceeds = false;
        Assert.False(coordinator.TryMoveToSlot(1, 0, DesktopSnapLayoutKind.Halves, 1, 40, 0));
        Assert.Equal((100, 40, 1920, 1040), (window.CanvasX, window.CanvasY, window.Width, window.Height));
        Assert.Equal(0, scroller.RepositionCount);
    }


    [Fact]
    public void RepeatedMovePreparationRestoresOnlyOnce()
    {
        DesktopWindowPlacementCoordinator coordinator = CreateCoordinator();
        AddWindow(1, 100, 40, 1920, 1040);
        windowStateController.State = new(true, false, true);
        Assert.True(coordinator.TryPrepareForMove(1, out _));
        Assert.True(coordinator.TryPrepareForMove(1, out _));
        Assert.Single(windowStateController.Commands);
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
        List<nint[]> animationBatches = [];
        int completedBatches = 0;
        coordinator.PlacementStarting += handles => animationBatches.Add([..handles]);
        coordinator.PlacementCompleted += _ => completedBatches++;
        int moved = coordinator.MoveByPages([first.Handle, second.Handle], 1, maximumPageCount: null);
        Assert.Equal(2, moved);
        Assert.Equal(2040, first.CanvasX);
        Assert.Equal(2820, second.CanvasX);
        Assert.Equal(1, scroller.RepositionCount);
        Assert.Equal(new nint[] { 1, 2 }, Assert.Single(animationBatches));
        Assert.Equal(1, completedBatches);
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
        DesktopSnapSlotOccupancyResolver occupancyResolver = new(new(geometryReader));
        DesktopSnapPlacementResolver placementResolver = new(workspace, new DesktopSnapLayoutCatalog());
        return new(store, scroller, workspace, resizeSynchronizer, windowCloser, windowStateController, pageTransitionGuard, placementResolver, occupancyResolver, new(geometryReader));
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
        public nint ClosedHandle { get; private set; }


        public bool TryClose(nint windowHandle)
        {
            ClosedHandle = windowHandle;
            return true;
        }
    }


    private sealed class TestWindowStateController : IWindowStateController
    {
        public WindowCommandState State { get; set; } = new(true, true, false);

        public WindowRestoreBounds RestoreBounds { get; set; } = new(300, 140, 800, 600);

        public bool RestoreSucceeds { get; set; } = true;

        public bool TryRestoreForMove(nint windowHandle, out WindowRestoreBounds bounds)
        {
            Commands.Add((windowHandle, "RestoreForMove"));
            bounds = RestoreBounds;
            if (RestoreSucceeds)
            {
                State = new(true, true, false);
            }

            return RestoreSucceeds;
        }


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


    private sealed class TestWindowPageTransitionGuard : IWindowPageTransitionGuard
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

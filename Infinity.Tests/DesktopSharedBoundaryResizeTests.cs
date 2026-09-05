using Elysium.Platform.Abstractions;
using Infinity.Application;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopSharedBoundaryResizeTests
{
    [Theory]
    [InlineData(DesktopResizeEdge.Right, 1, 100)]
    [InlineData(DesktopResizeEdge.Left, 2, -100)]
    public void EitherSideOfVerticalJoinResizesBothWindows(DesktopResizeEdge edge, int source, int delta)
    {
        Fixture test = new();
        test.Add(1, new(0, 0, 500, 800));
        test.Add(2, new(500, 0, 500, 800));
        test.Drag(source, edge, delta);
        Assert.Equal(new(0, 0, 500 + delta, 800), test.Native.Bounds[1]);
        Assert.Equal(new(500 + delta, 0, 500 - delta, 800), test.Native.Bounds[2]);
    }

    [Theory]
    [InlineData(DesktopResizeEdge.Bottom, 1, 100)]
    [InlineData(DesktopResizeEdge.Top, 2, -100)]
    public void EitherSideOfHorizontalJoinResizesBothWindows(DesktopResizeEdge edge, int source, int delta)
    {
        Fixture test = new();
        test.Add(1, new(0, 0, 1000, 400));
        test.Add(2, new(0, 400, 1000, 400));
        test.Drag(source, edge, delta);
        Assert.Equal(new(0, 0, 1000, 400 + delta), test.Native.Bounds[1]);
        Assert.Equal(new(0, 400 + delta, 1000, 400 - delta), test.Native.Bounds[2]);
    }

    [Fact]
    public void TJunctionMovesTheWholeConnectedBoundary()
    {
        Fixture test = new();
        test.Add(1, new(0, 0, 500, 400));
        test.Add(2, new(0, 400, 500, 400));
        test.Add(3, new(500, 0, 500, 800));
        test.Drag(1, DesktopResizeEdge.Right, 100);
        Assert.Equal(new(0, 400, 600, 400), test.Native.Bounds[2]);
        Assert.Equal(new(600, 0, 400, 800), test.Native.Bounds[3]);
    }

    [Fact]
    public void DifferentInvisibleBordersStayAligned()
    {
        Fixture test = new();
        test.Native.Insets[1] = (8, 0, 8, 8);
        test.Add(1, new(-8, 0, 516, 808));
        test.Add(2, new(500, 0, 500, 800));
        test.Drag(1, DesktopResizeEdge.Right, 100);
        Assert.Equal(new(-8, 0, 616, 808), test.Native.Bounds[1]);
        Assert.Equal(new(600, 0, 400, 800), test.Native.Bounds[2]);
    }

    [Fact]
    public void NeighboursMinimumSizeClampsBothSidesAndRepeatedPulls()
    {
        Fixture test = new();
        test.Add(1, new(0, 0, 500, 800));
        test.Add(2, new(500, 0, 500, 800));
        test.Native.Limits[2] = new(350, 100, 2000, 2000);
        test.Events.Begin(1);
        test.Native.Bounds[1] = new(0, 0, 800, 800);
        test.Events.Change(1);
        Assert.Equal(new(0, 0, 650, 800), test.Native.Bounds[1]);
        Assert.Equal(new(650, 0, 350, 800), test.Native.Bounds[2]);
        test.Native.Bounds[1] = new(0, 0, 850, 800);
        test.Events.Change(1);
        Assert.Equal(new(0, 0, 650, 800), test.Native.Bounds[1]);
        test.Events.End(1);
    }

    [Fact]
    public void MaximumSizeIsRespected()
    {
        Fixture test = new();
        test.Add(1, new(0, 0, 500, 800));
        test.Add(2, new(500, 0, 500, 800));
        test.Native.Limits[2] = new(100, 100, 550, 2000);
        test.Drag(1, DesktopResizeEdge.Right, -200);
        Assert.Equal(new(0, 0, 450, 800), test.Native.Bounds[1]);
        Assert.Equal(new(450, 0, 550, 800), test.Native.Bounds[2]);
    }

    [Fact]
    public void EscapeRestoresTheNeighboursWhenWindowsRestoresTheSource()
    {
        Fixture test = new();
        test.Add(1, new(0, 0, 500, 800));
        test.Add(2, new(500, 0, 500, 800));
        test.Events.Begin(1);
        test.Native.Bounds[1] = new(0, 0, 600, 800);
        test.Events.Change(1);
        test.Native.Bounds[1] = new(0, 0, 500, 800);
        test.Events.End(1);
        Assert.Equal(new(500, 0, 500, 800), test.Native.Bounds[2]);
    }

    [Fact]
    public void TitleBarMovementAndCornerResizesDoNotMoveNeighbours()
    {
        foreach (DesktopSnapPlacement moved in new DesktopSnapPlacement[] { new(20, 0, 500, 800), new(0, 0, 600, 700) })
        {
            Fixture test = new();
            test.Add(1, new(0, 0, 500, 800));
            test.Add(2, new(500, 0, 500, 800));
            test.Events.Begin(1);
            test.Native.Bounds[1] = moved;
            test.Events.Change(1);
            test.Events.End(1);
            Assert.Empty(test.Native.Requests);
        }
    }

    [Fact]
    public void OverlayAndProgrammaticResizesDoNotEngageSharedResizing()
    {
        Fixture test = new();
        test.Add(1, new(0, 0, 500, 800));
        test.Add(2, new(500, 0, 500, 800));
        test.Native.Bounds[1] = new(0, 0, 600, 800);
        test.Events.Change(1);
        Assert.Empty(test.Native.Requests);
        test.Native.Bounds[1] = new(0, 0, 500, 800);
        test.Presentation.Begin();
        test.Drag(1, DesktopResizeEdge.Right, 100);
        Assert.Empty(test.Native.Requests);
    }

    [Fact]
    public void UnsupportedOrUnresponsiveNeighbourIsNotResized()
    {
        Fixture test = new();
        test.Add(1, new(0, 0, 500, 800));
        test.Add(2, new(500, 0, 500, 800));
        test.Native.Unsupported.Add(2);
        test.Drag(1, DesktopResizeEdge.Right, 100);
        Assert.Empty(test.Native.Requests);
    }

    [Fact]
    public void SeparatePagesCannotBecomeLinked()
    {
        Fixture test = new();
        test.Add(1, new(500, 0, 500, 800));
        test.Add(2, new(1000, 0, 500, 800));
        test.Drag(1, DesktopResizeEdge.Right, 100);
        Assert.Empty(test.Native.Requests);
    }

    [Fact]
    public void PageOffsetIsPreservedInTrackedGeometry()
    {
        Fixture test = new();
        test.Pan.SetOffset(1000);
        test.Add(1, new(0, 0, 500, 800));
        test.Add(2, new(500, 0, 500, 800));
        test.Drag(1, DesktopResizeEdge.Right, 100);
        Assert.True(test.Store.TryGet(2, out TrackedWindow window));
        Assert.Equal(1600, window.CanvasX);
        Assert.Equal(600, window.LastPlacedX);
        Assert.Equal(400, window.Width);
    }

    [Fact]
    public void ClosingParticipantOrStoppingCancelsTheSession()
    {
        foreach (bool stop in new[] { false, true })
        {
            Fixture test = new();
            test.Add(1, new(0, 0, 500, 800));
            test.Add(2, new(500, 0, 500, 800));
            test.Events.Begin(1);
            test.Native.Bounds[1] = new(0, 0, 600, 800);
            test.Events.Change(1);
            test.Native.Requests.Clear();
            if (stop)
            {
                test.Coordinator.Stop();
            }
            else
            {
                test.Events.Destroy(2);
            }

            test.Native.Bounds[1] = new(0, 0, 700, 800);
            test.Events.Change(1);
            Assert.Empty(test.Native.Requests);
        }
    }

    [Fact]
    public void OverlappingCandidatesDoNotProduceAmbiguousGroups()
    {
        IReadOnlyList<DesktopSharedBoundaryMember> group = DesktopSharedBoundaryCalculator.FindGroup(1, DesktopResizeEdge.Right,
            [(1, new(0, 0, 500, 800)), (2, new(500, 0, 500, 800)), (3, new(500, 0, 500, 800))]);
        Assert.Empty(group);
    }

    [Fact]
    public void DisconnectedAndCornerTouchingWindowsAreNotLinked()
    {
        IReadOnlyList<DesktopSharedBoundaryMember> group = DesktopSharedBoundaryCalculator.FindGroup(1, DesktopResizeEdge.Right,
            [(1, new(0, 0, 500, 400)), (2, new(500, 400, 500, 400)), (3, new(500, 500, 500, 400))]);
        Assert.Empty(group);
    }

    [Fact]
    public void DelayedNativeUpdatesAreNotMistakenForNewUserResizes()
    {
        Fixture test = new();
        test.Add(1, new(0, 0, 500, 800));
        test.Add(2, new(500, 0, 500, 800));
        test.Native.Delay = true;
        test.Events.Begin(1);
        test.Native.Bounds[1] = new(0, 0, 600, 800);
        test.Events.Change(1);
        test.Events.Change(1);
        Assert.Single(test.Native.Requests);
        test.Native.Bounds[1] = new(0, 0, 650, 800);
        test.Events.Change(1);
        test.Native.Flush();
        test.Events.Change(2);
        test.Events.End(1);
        Assert.Equal(new(650, 0, 350, 800), test.Native.Bounds[2]);
        Assert.Equal(2, test.Native.Requests.Count);
    }

    [Fact]
    public void ChangingPageDuringResizeCancelsTheSession()
    {
        Fixture test = new();
        test.Add(1, new(0, 0, 500, 800));
        test.Add(2, new(500, 0, 500, 800));
        test.Events.Begin(1);
        test.Pan.SetOffset(1000);
        test.Native.Bounds[1] = new(0, 0, 600, 800);
        test.Events.Change(1);
        Assert.Empty(test.Native.Requests);
    }

    [Fact]
    public void EscapeSupersedesAnOutstandingNativeResize()
    {
        Fixture test = new();
        test.Add(1, new(0, 0, 500, 800));
        test.Add(2, new(500, 0, 500, 800));
        test.Native.Delay = true;
        test.Events.Begin(1);
        test.Native.Bounds[1] = new(0, 0, 600, 800);
        test.Events.Change(1);
        test.Native.Bounds[1] = new(0, 0, 500, 800);
        test.Events.End(1);
        test.Native.Flush();
        Assert.Equal(new(500, 0, 500, 800), test.Native.Bounds[2]);
        Assert.Equal(2, test.Native.Requests.Count);
    }

    [Fact]
    public void DelayedDragStartUsesTheTrackedPreDragBounds()
    {
        Fixture test = new();
        test.Add(1, new(0, 0, 500, 800));
        test.Add(2, new(500, 0, 500, 800));
        test.Native.Bounds[1] = new(0, 0, 600, 800);
        test.Events.Begin(1);
        test.Events.Change(1);
        test.Events.End(1);
        Assert.Equal(new(600, 0, 400, 800), test.Native.Bounds[2]);
    }

    private sealed class Fixture
    {
        public WindowStore Store { get; } = new();
        public NativeWindows Native { get; } = new();
        public WindowEvents Events { get; } = new();
        public PanState Pan { get; } = new();
        public ScrollPresentationSession Presentation { get; } = new();
        public DesktopSharedBoundaryResizeCoordinator Coordinator { get; }

        public Fixture()
        {
            Coordinator = new(Store, Events, Native, Native, new(Native), new Workspace(), Pan, Presentation);
            Coordinator.Start();
        }

        public void Add(nint handle, DesktopSnapPlacement bounds)
        {
            Native.Bounds[handle] = bounds;
            Store.Add(new() { Handle = handle, CanvasX = (int)(bounds.CanvasX + Pan.Offset), CanvasY = (int)bounds.CanvasY, Width = (int)bounds.Width, Height = (int)bounds.Height });
        }

        public void Drag(nint handle, DesktopResizeEdge edge, double delta)
        {
            Events.Begin(handle);
            Native.Bounds[handle] = DesktopSharedBoundaryCalculator.Resize(Native.Bounds[handle], edge, delta);
            Events.Change(handle);
            Events.End(handle);
        }
    }

    private sealed class NativeWindows : IWindowGeometryReader, IWindowBoundaryResizer
    {
        public Dictionary<nint, DesktopSnapPlacement> Bounds { get; } = [];
        public Dictionary<nint, WindowResizeLimits> Limits { get; } = [];
        public Dictionary<nint, (int Left, int Top, int Right, int Bottom)> Insets { get; } = [];
        public HashSet<nint> Unsupported { get; } = [];
        public List<nint> Requests { get; } = [];
        public bool Delay { get; set; }
        private readonly Queue<(nint Handle, DesktopSnapPlacement Bounds)> pending = [];

        public bool TryGetLimits(nint handle, out WindowResizeLimits limits)
        {
            limits = Limits.GetValueOrDefault(handle, new(100, 100, 3000, 3000));
            return !Unsupported.Contains(handle);
        }

        public bool TryResize(nint handle, int x, int y, int width, int height)
        {
            Requests.Add(handle);
            if (Delay)
            {
                pending.Enqueue((handle, new(x, y, width, height)));
            }
            else
            {
                Bounds[handle] = new(x, y, width, height);
            }

            return true;
        }

        public void Flush()
        {
            while (pending.TryDequeue(out (nint Handle, DesktopSnapPlacement Bounds) update))
            {
                Bounds[update.Handle] = update.Bounds;
            }
        }

        public bool IsVisible(nint handle) => Bounds.ContainsKey(handle);
        public bool IsMinimised(nint handle) => false;

        public bool TryReadGeometry(nint handle, out int x, out int y, out int width, out int height)
        {
            bool found = Bounds.TryGetValue(handle, out DesktopSnapPlacement bounds);
            x = (int)bounds.CanvasX;
            y = (int)bounds.CanvasY;
            width = (int)bounds.Width;
            height = (int)bounds.Height;
            return found;
        }

        public bool TryReadVisibleGeometry(nint handle, out int x, out int y, out int width, out int height)
        {
            bool found = TryReadGeometry(handle, out x, out y, out width, out height);
            (int left, int top, int right, int bottom) = Insets.GetValueOrDefault(handle);
            x += left;
            y += top;
            width -= left + right;
            height -= top + bottom;
            return found;
        }
    }

    private sealed class Workspace : IWorkspace
    {
        public event EventHandler? WorkspaceLayoutChanged;
        public int Width => 1000;
        public int Height => 800;
        public int WorkAreaX => 0;
        public int WorkAreaY => 0;
        public nint GetCurrentWorkspace() => 0;
    }

    private sealed class WindowEvents : IWindowEventListener
    {
        public event Action<nint>? WindowCreated;
        public event Action<nint>? WindowShown;
        public event Action<nint>? WindowDestroyed;
        public event Action<nint>? WindowTitleChanged;
        public event Action<nint>? WindowLocationChanged;
        public event Action<nint>? MinimizeStarted;
        public event Action<nint>? MinimizeEnded;
        public event Action<nint>? DragStarted;
        public event Action<nint>? DragEnded;
        public event Action<nint>? ForegroundChanged;
        public event Action? WindowStackChanged;

        public void Begin(nint handle) => DragStarted?.Invoke(handle);
        public void Change(nint handle) => WindowLocationChanged?.Invoke(handle);
        public void End(nint handle) => DragEnded?.Invoke(handle);
        public void Destroy(nint handle) => WindowDestroyed?.Invoke(handle);
        public void Start() { }
        public void Stop() { }
        public void Dispose() => GC.SuppressFinalize(this);
    }
}

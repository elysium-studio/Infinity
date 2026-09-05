using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;

namespace Infinity.Shell;

public sealed class DesktopSharedBoundaryResizeCoordinator(IWindowStore windows, IWindowEventListener events, IWindowGeometryReader geometry, IWindowBoundaryResizer resizer, DesktopWindowFrameGeometry frames, IWorkspace workspace, IPanState pan, IScrollPresentationSession presentation)
{
    private readonly Dictionary<nint, DesktopSnapPlacement> originals = [];
    private readonly List<(nint Handle, DesktopSnapPlacement Bounds)> visibleWindows = [];
    private readonly List<Member> members = [];
    private nint source;
    private DesktopSnapPlacement sourceBounds;
    private DesktopResizeEdge? resizeEdge;
    private double minimumDelta;
    private double maximumDelta;
    private bool started;

    public void Start()
    {
        if (started)
        {
            return;
        }

        started = true;
        events.DragStarted += Begin;
        events.DragEnded += End;
        events.WindowLocationChanged += Update;
        events.WindowDestroyed += HandleDestroyed;
        events.MinimizeStarted += HandleDestroyed;
        workspace.WorkspaceLayoutChanged += HandleWorkspaceChanged;
        pan.OffsetChanged += Clear;
    }

    public void Stop()
    {
        if (!started)
        {
            return;
        }

        started = false;
        events.DragStarted -= Begin;
        events.DragEnded -= End;
        events.WindowLocationChanged -= Update;
        events.WindowDestroyed -= HandleDestroyed;
        events.MinimizeStarted -= HandleDestroyed;
        workspace.WorkspaceLayoutChanged -= HandleWorkspaceChanged;
        pan.OffsetChanged -= Clear;
        Clear();
    }

    private void Begin(nint handle)
    {
        Clear();
        if (presentation.IsActive || workspace.Width <= 0 || !windows.TryGet(handle, out TrackedWindow? window))
        {
            return;
        }

        int page = GetPage(window);
        foreach (TrackedWindow candidate in windows)
        {
            if (GetPage(candidate) != page || !TryRead(candidate.Handle, out DesktopSnapPlacement outer))
            {
                continue;
            }

            if (candidate.Handle == handle && candidate.Width > 0 && candidate.Height > 0)
            {
                outer = new(candidate.CanvasX - Math.Round(pan.Offset), candidate.CanvasY, candidate.Width, candidate.Height);
            }

            DesktopSnapPlacement visible = frames.ToVisible(candidate.Handle, outer);
            originals.Add(candidate.Handle, outer);
            visibleWindows.Add((candidate.Handle, visible));
            if (candidate.Handle == handle)
            {
                source = handle;
                sourceBounds = visible;
            }
        }
    }

    private void Update(nint handle)
    {
        if (source == 0)
        {
            return;
        }

        if (presentation.IsActive || !windows.TryGet(source, out _))
        {
            Clear();
            return;
        }

        if (handle != source)
        {
            if (members.Any(member => member.Boundary.Handle == handle))
            {
                Synchronise(handle);
            }

            return;
        }

        if (!TryRead(source, out DesktopSnapPlacement outer))
        {
            Clear();
            return;
        }

        DesktopSnapPlacement originalOuter = originals[source];
        DesktopSnapPlacement current = new(outer.CanvasX + sourceBounds.CanvasX - originalOuter.CanvasX, outer.CanvasY + sourceBounds.CanvasY - originalOuter.CanvasY, outer.Width + sourceBounds.Width - originalOuter.Width, outer.Height + sourceBounds.Height - originalOuter.Height);
        DesktopResizeEdge? changed = DesktopSharedBoundaryCalculator.FindChangedEdge(sourceBounds, current);
        if (current == sourceBounds && !resizeEdge.HasValue)
        {
            return;
        }

        if (current != sourceBounds && (!changed.HasValue || resizeEdge.HasValue && changed != resizeEdge))
        {
            Clear();
            return;
        }

        if (!resizeEdge.HasValue && (!changed.HasValue || !TryCreateGroup(changed.Value)))
        {
            Clear();
            return;
        }

        DesktopResizeEdge edge = resizeEdge!.Value;
        double requested = DesktopSharedBoundaryCalculator.Coordinate(current, edge) - DesktopSharedBoundaryCalculator.Coordinate(sourceBounds, edge);
        double delta = Math.Clamp(requested, minimumDelta, maximumDelta);
        foreach (Member member in members)
        {
            if (!windows.TryGet(member.Boundary.Handle, out _) || !TryRead(member.Boundary.Handle, out DesktopSnapPlacement actual))
            {
                Clear();
                return;
            }

            DesktopSnapPlacement target = DesktopSharedBoundaryCalculator.Resize(member.Outer, member.Boundary.Edge, delta);
            if (actual == target && (!member.LastRequested.HasValue || member.LastRequested == target) || member.Boundary.Handle != source && member.LastRequested == target)
            {
                continue;
            }

            if (!resizer.TryResize(member.Boundary.Handle, (int)target.CanvasX, (int)target.CanvasY, (int)target.Width, (int)target.Height))
            {
                Clear();
                return;
            }

            member.LastRequested = target;
        }

        Synchronise(handle);
    }

    private bool TryCreateGroup(DesktopResizeEdge edge)
    {
        IReadOnlyList<DesktopSharedBoundaryMember> group = DesktopSharedBoundaryCalculator.FindGroup(source, edge, visibleWindows);
        if (group.Count < 2)
        {
            return false;
        }

        minimumDelta = double.NegativeInfinity;
        maximumDelta = double.PositiveInfinity;
        foreach (DesktopSharedBoundaryMember boundary in group)
        {
            if (!resizer.TryGetLimits(boundary.Handle, out WindowResizeLimits limits))
            {
                return false;
            }

            DesktopSnapPlacement outer = originals[boundary.Handle];
            bool vertical = DesktopSharedBoundaryCalculator.IsVertical(edge);
            double size = vertical ? outer.Width : outer.Height;
            double minimumSize = Math.Min(size, vertical ? limits.MinimumWidth : limits.MinimumHeight);
            double maximumSize = Math.Max(size, vertical ? limits.MaximumWidth : limits.MaximumHeight);
            bool grows = boundary.Edge is DesktopResizeEdge.Right or DesktopResizeEdge.Bottom;
            minimumDelta = Math.Max(minimumDelta, grows ? minimumSize - size : size - maximumSize);
            maximumDelta = Math.Min(maximumDelta, grows ? maximumSize - size : size - minimumSize);
            members.Add(new(boundary, outer));
        }

        resizeEdge = edge;
        return minimumDelta <= maximumDelta;
    }

    private void End(nint handle)
    {
        if (source != handle)
        {
            return;
        }

        Update(handle);
        foreach (Member member in members)
        {
            Synchronise(member.Boundary.Handle);
        }

        Clear();
    }

    private bool TryRead(nint handle, out DesktopSnapPlacement bounds)
    {
        bounds = default;
        if (!geometry.IsVisible(handle) || geometry.IsMinimised(handle) || !geometry.TryReadGeometry(handle, out int x, out int y, out int width, out int height) || width <= 0 || height <= 0)
        {
            return false;
        }

        bounds = new(x, y, width, height);
        return true;
    }

    private void Synchronise(nint handle)
    {
        if (!windows.TryGet(handle, out TrackedWindow? window) || !TryRead(handle, out DesktopSnapPlacement actual))
        {
            return;
        }

        int canvasX = (int)(actual.CanvasX + Math.Round(pan.Offset));
        if (window.CanvasX == canvasX && window.CanvasY == actual.CanvasY && window.Width == actual.Width && window.Height == actual.Height)
        {
            return;
        }

        window.CanvasX = canvasX;
        window.CanvasY = (int)actual.CanvasY;
        window.Width = (int)actual.Width;
        window.Height = (int)actual.Height;
        window.LastPlacedX = (int)actual.CanvasX;
        window.LastPlacedY = (int)actual.CanvasY;
        windows.NotifyChanged(handle);
    }

    private int GetPage(TrackedWindow window) => (int)Math.Floor((window.CanvasX - (double)workspace.WorkAreaX + window.Width / 2d) / workspace.Width);

    private void HandleWorkspaceChanged(object? sender, EventArgs args) => Clear();

    private void HandleDestroyed(nint handle)
    {
        if (source == handle || members.Any(member => member.Boundary.Handle == handle))
        {
            Clear();
        }
    }

    private void Clear()
    {
        source = 0;
        resizeEdge = null;
        originals.Clear();
        visibleWindows.Clear();
        members.Clear();
    }

    private sealed class Member(DesktopSharedBoundaryMember boundary, DesktopSnapPlacement outer)
    {
        public DesktopSharedBoundaryMember Boundary { get; } = boundary;

        public DesktopSnapPlacement Outer { get; } = outer;

        public DesktopSnapPlacement? LastRequested { get; set; }
    }
}

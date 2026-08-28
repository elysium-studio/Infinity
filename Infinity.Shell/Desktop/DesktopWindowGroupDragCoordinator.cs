using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;

namespace Infinity.Shell;

public sealed class DesktopWindowGroupDragCoordinator(
    IWindowStore windowStore,
    IWorkspace workspace,
    IPager pager,
    DesktopWindowDragPositionResolver dragPositionResolver,
    DesktopSnapSlotOccupancyResolver occupancyResolver,
    DesktopWindowPlacementCoordinator placementCoordinator)
{
    private const double PlacementTolerance = 2;

    private readonly Dictionary<nint, DesktopSnapPlacement> sourcePlacements = [];
    private nint leaderHandle;

    public bool IsActive => leaderHandle != 0 && sourcePlacements.Count > 1;

    public bool Begin(nint leader, IEnumerable<nint> handles)
    {
        Cancel();

        foreach (nint handle in handles.Where(handle => handle != 0).Distinct())
        {
            if (windowStore.TryGet(handle, out TrackedWindow? window))
            {
                sourcePlacements.Add(handle, GetPlacement(window));
            }
        }

        if (!sourcePlacements.ContainsKey(leader) || sourcePlacements.Count < 2)
        {
            Cancel();
            return false;
        }

        leaderHandle = leader;
        return true;
    }

    public bool Complete(nint leader, double horizontalVisualDelta, double verticalVisualDelta, DesktopSnapPlacement? snapPlacement)
    {
        if (!IsActive || leader != leaderHandle || workspace.Width <= 0 || workspace.Height <= 0 || !sourcePlacements.TryGetValue(leader, out DesktopSnapPlacement leaderSource))
        {
            Cancel();
            return false;
        }

        try
        {
            if (!TryResolveLeaderTarget(leader, horizontalVisualDelta, verticalVisualDelta, snapPlacement, leaderSource, out DesktopSnapPlacement leaderTarget))
            {
                return false;
            }

            int pageDelta = GetPage(leaderTarget) - GetPage(leaderSource);
            double horizontalDelta = leaderTarget.CanvasX - leaderSource.CanvasX;
            double verticalDelta = leaderTarget.CanvasY - leaderSource.CanvasY;
            HashSet<nint> selectedHandles = [.. sourcePlacements.Keys];
            List<(TrackedWindow Window, DesktopSnapPlacement Placement)> placements = [];

            foreach ((nint handle, DesktopSnapPlacement source) in sourcePlacements)
            {
                if (!windowStore.TryGet(handle, out TrackedWindow? window))
                {
                    return false;
                }

                DesktopSnapPlacement target;
                int targetPage = GetPage(source) + pageDelta;

                if (targetPage < 0 || pager.MaxPages.HasValue && targetPage >= pager.MaxPages.Value)
                {
                    return false;
                }

                if (handle == leader)
                {
                    target = leaderTarget;
                }
                else if (snapPlacement.HasValue)
                {
                    target = MoveToRelativePage(source, targetPage, pageDelta * (double)workspace.Width, 0);
                }
                else
                {
                    target = MoveToRelativePage(source, targetPage, horizontalDelta, verticalDelta);
                }

                placements.Add((window, target));
            }

            if (HasDuplicateDestinations(placements) || HasFollowerConflict(placements, leader, selectedHandles))
            {
                return false;
            }

            if (occupancyResolver.TryGetOccupant(leaderTarget, selectedHandles, windowStore, out TrackedWindow? occupant) && occupant is not null)
            {
                if (placements.Any(item => item.Window.Handle != leader && IsSamePlacement(item.Placement, leaderSource)))
                {
                    return false;
                }

                placements.Add((occupant, leaderSource));
            }

            if (placements.All(item => sourcePlacements.TryGetValue(item.Window.Handle, out DesktopSnapPlacement source) && IsSamePlacement(source, item.Placement)))
            {
                return false;
            }

            placementCoordinator.ApplyPlacements(placements);
            return true;
        }
        finally
        {
            Cancel();
        }
    }

    public void Cancel()
    {
        sourcePlacements.Clear();
        leaderHandle = 0;
    }

    private bool TryResolveLeaderTarget(nint leader, double horizontalVisualDelta, double verticalVisualDelta, DesktopSnapPlacement? snapPlacement, DesktopSnapPlacement source, out DesktopSnapPlacement target)
    {
        if (snapPlacement.HasValue)
        {
            target = snapPlacement.Value;
            return true;
        }

        if (!dragPositionResolver.TryResolve(leader, horizontalVisualDelta, verticalVisualDelta, out DesktopWindowDragPosition position))
        {
            target = default;
            return false;
        }

        target = new DesktopSnapPlacement(position.CanvasX, position.CanvasY, source.Width, source.Height);
        return true;
    }

    private DesktopSnapPlacement MoveToRelativePage(DesktopSnapPlacement source, int targetPage, double horizontalDelta, double verticalDelta)
    {
        double pageLeft = workspace.WorkAreaX + (targetPage * (double)workspace.Width);
        double targetX = source.CanvasX + horizontalDelta;
        double targetY = source.CanvasY + verticalDelta;

        return new DesktopSnapPlacement(
            Math.Clamp(targetX, pageLeft, pageLeft + Math.Max(0, workspace.Width - source.Width)),
            Math.Clamp(targetY, workspace.WorkAreaY, workspace.WorkAreaY + Math.Max(0, workspace.Height - source.Height)),
            source.Width,
            source.Height);
    }

    private bool HasFollowerConflict(IEnumerable<(TrackedWindow Window, DesktopSnapPlacement Placement)> placements, nint leader, IReadOnlySet<nint> selectedHandles)
        => placements.Any(item => item.Window.Handle != leader && occupancyResolver.IsOccupied(item.Placement, selectedHandles, windowStore));

    private static bool HasDuplicateDestinations(IReadOnlyList<(TrackedWindow Window, DesktopSnapPlacement Placement)> placements)
    {
        for (int first = 0; first < placements.Count; first++)
        {
            for (int second = first + 1; second < placements.Count; second++)
            {
                if (IsSamePlacement(placements[first].Placement, placements[second].Placement))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private int GetPage(DesktopSnapPlacement placement)
    {
        double center = placement.CanvasX - workspace.WorkAreaX + (placement.Width / 2d);
        return Math.Max(0, (int)Math.Clamp(Math.Floor(center / workspace.Width), 0, int.MaxValue));
    }

    private static DesktopSnapPlacement GetPlacement(TrackedWindow window) => new(window.CanvasX, window.CanvasY, window.Width, window.Height);

    private static bool IsSamePlacement(DesktopSnapPlacement first, DesktopSnapPlacement second)
        => Math.Abs(first.CanvasX - second.CanvasX) <= PlacementTolerance &&
           Math.Abs(first.CanvasY - second.CanvasY) <= PlacementTolerance &&
           Math.Abs(first.Width - second.Width) <= PlacementTolerance &&
           Math.Abs(first.Height - second.Height) <= PlacementTolerance;
}

namespace Infinity.Shell;

public static class DesktopSharedBoundaryCalculator
{
    private const double Tolerance = 2;
    private static readonly DesktopResizeEdge[] Edges = Enum.GetValues<DesktopResizeEdge>();

    public static DesktopResizeEdge? FindChangedEdge(DesktopSnapPlacement original, DesktopSnapPlacement current)
    {
        DesktopResizeEdge? changed = null;
        foreach (DesktopResizeEdge edge in Edges)
        {
            if (Math.Abs(Coordinate(current, edge) - Coordinate(original, edge)) < 0.5)
            {
                continue;
            }

            if (changed.HasValue)
            {
                return null;
            }

            changed = edge;
        }

        return changed;
    }

    public static IReadOnlyList<DesktopSharedBoundaryMember> FindGroup(nint source, DesktopResizeEdge edge, IReadOnlyList<(nint Handle, DesktopSnapPlacement Bounds)> windows)
    {
        (nint Handle, DesktopSnapPlacement Bounds) initial = windows.FirstOrDefault(window => window.Handle == source);
        if (initial.Handle == 0)
        {
            return [];
        }

        List<DesktopSharedBoundaryMember> members = [new(source, initial.Bounds, edge)];
        for (int index = 0; index < members.Count; index++)
        {
            DesktopSharedBoundaryMember member = members[index];
            DesktopResizeEdge opposite = Opposite(member.Edge);
            foreach ((nint handle, DesktopSnapPlacement bounds) in windows)
            {
                if (members.Any(existing => existing.Handle == handle) || Math.Abs(Coordinate(bounds, opposite) - Coordinate(initial.Bounds, edge)) > Tolerance || Overlap(member.Bounds, bounds, edge) <= Tolerance)
                {
                    continue;
                }

                if (members.Any(existing => existing.Edge == opposite && Overlap(existing.Bounds, bounds, edge) > Tolerance))
                {
                    return [];
                }

                members.Add(new(handle, bounds, opposite));
            }
        }

        return members.Count > 1 ? members : [];
    }

    public static DesktopSnapPlacement Resize(DesktopSnapPlacement bounds, DesktopResizeEdge edge, double delta) => edge switch
    {
        DesktopResizeEdge.Left => bounds with { CanvasX = bounds.CanvasX + delta, Width = bounds.Width - delta },
        DesktopResizeEdge.Right => bounds with { Width = bounds.Width + delta },
        DesktopResizeEdge.Top => bounds with { CanvasY = bounds.CanvasY + delta, Height = bounds.Height - delta },
        DesktopResizeEdge.Bottom => bounds with { Height = bounds.Height + delta },
        _ => bounds
    };

    public static double Coordinate(DesktopSnapPlacement bounds, DesktopResizeEdge edge) => edge switch
    {
        DesktopResizeEdge.Left => bounds.CanvasX,
        DesktopResizeEdge.Right => bounds.CanvasX + bounds.Width,
        DesktopResizeEdge.Top => bounds.CanvasY,
        DesktopResizeEdge.Bottom => bounds.CanvasY + bounds.Height,
        _ => throw new ArgumentOutOfRangeException(nameof(edge))
    };

    public static bool IsVertical(DesktopResizeEdge edge) => edge is DesktopResizeEdge.Left or DesktopResizeEdge.Right;

    private static DesktopResizeEdge Opposite(DesktopResizeEdge edge) => edge switch
    {
        DesktopResizeEdge.Left => DesktopResizeEdge.Right,
        DesktopResizeEdge.Right => DesktopResizeEdge.Left,
        DesktopResizeEdge.Top => DesktopResizeEdge.Bottom,
        DesktopResizeEdge.Bottom => DesktopResizeEdge.Top,
        _ => throw new ArgumentOutOfRangeException(nameof(edge))
    };

    private static double Overlap(DesktopSnapPlacement first, DesktopSnapPlacement second, DesktopResizeEdge edge) => IsVertical(edge)
        ? Math.Min(first.CanvasY + first.Height, second.CanvasY + second.Height) - Math.Max(first.CanvasY, second.CanvasY)
        : Math.Min(first.CanvasX + first.Width, second.CanvasX + second.Width) - Math.Max(first.CanvasX, second.CanvasX);
}

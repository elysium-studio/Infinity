namespace Infinity.Shell;

public static class DesktopSharedBoundaryVisual
{
    public const double Thickness = 12;

    public static DesktopSnapPlacement GetBounds(IReadOnlyList<DesktopSharedBoundaryMember> members, double thickness, double delta = 0)
    {
        if (members.Count < 2 || !double.IsFinite(thickness) || thickness <= 0)
        {
            return default;
        }

        DesktopSharedBoundaryMember first = members[0];
        bool vertical = DesktopSharedBoundaryCalculator.IsVertical(first.Edge);
        double start = members.Min(member => vertical ? member.Bounds.CanvasY : member.Bounds.CanvasX);
        double end = members.Max(member => vertical ? member.Bounds.CanvasY + member.Bounds.Height : member.Bounds.CanvasX + member.Bounds.Width);
        double line = DesktopSharedBoundaryCalculator.Coordinate(first.Bounds, first.Edge) + delta;
        return vertical ? new(line - thickness / 2, start, thickness, end - start) : new(start, line - thickness / 2, end - start, thickness);
    }
}

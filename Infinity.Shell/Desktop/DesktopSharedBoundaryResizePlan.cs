using Infinity.Platform.Abstractions;

namespace Infinity.Shell;

public sealed class DesktopSharedBoundaryResizePlan
{
    private readonly double minimumDelta;
    private readonly double maximumDelta;

    public DesktopSharedBoundaryResizePlan(IReadOnlyList<(DesktopSharedBoundaryMember Member, WindowResizeLimits Limits)> windows)
    {
        if (windows.Count < 2)
        {
            throw new ArgumentException("A shared boundary requires at least two windows.", nameof(windows));
        }

        Members = windows.Select(window => window.Member).ToArray();
        minimumDelta = double.NegativeInfinity;
        maximumDelta = double.PositiveInfinity;
        foreach ((DesktopSharedBoundaryMember member, WindowResizeLimits limits) in windows)
        {
            bool vertical = DesktopSharedBoundaryCalculator.IsVertical(member.Edge);
            double size = vertical ? member.Bounds.Width : member.Bounds.Height;
            double minimum = Math.Min(size, vertical ? limits.MinimumWidth : limits.MinimumHeight);
            double maximum = Math.Max(size, vertical ? limits.MaximumWidth : limits.MaximumHeight);
            bool grows = member.Edge is DesktopResizeEdge.Right or DesktopResizeEdge.Bottom;
            minimumDelta = Math.Max(minimumDelta, grows ? minimum - size : size - maximum);
            maximumDelta = Math.Min(maximumDelta, grows ? maximum - size : size - minimum);
        }
    }

    public IReadOnlyList<DesktopSharedBoundaryMember> Members { get; }

    public double Delta { get; private set; }

    public void Update(double pointerDistance, double overviewScale)
    {
        if (double.IsFinite(pointerDistance) && double.IsFinite(overviewScale) && overviewScale > 0)
        {
            Delta = Math.Clamp(Math.Round(pointerDistance / overviewScale), minimumDelta, maximumDelta);
        }
    }

    public DesktopSnapPlacement GetPlacement(DesktopSharedBoundaryMember member) => DesktopSharedBoundaryCalculator.Resize(member.Bounds, member.Edge, Delta);
}

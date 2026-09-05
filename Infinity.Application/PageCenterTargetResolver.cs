using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;

namespace Infinity.Application;

public sealed class PageCenterTargetResolver(IWorkspace workspace) : IPageCenterTargetResolver
{
    private const double AlignmentTolerance = 0.5;

    public bool TryResolve(double offset, double minimumOffset, double maximumOffset, out double targetOffset)
    {
        targetOffset = offset;
        if (!double.IsFinite(offset) || workspace.Width <= 0)
        {
            return false;
        }

        double page = Math.Round(offset / workspace.Width);
        targetOffset = Math.Clamp(page * workspace.Width, minimumOffset, maximumOffset);
        return Math.Abs(targetOffset - offset) >= AlignmentTolerance;
    }


    public bool TryResolveAdjacent(double offset, int pageDelta, double minimumOffset, double maximumOffset, out double targetOffset)
    {
        targetOffset = offset;
        if (!double.IsFinite(offset) || workspace.Width <= 0 || pageDelta == 0)
        {
            return false;
        }

        double currentPage = Math.Round(offset / workspace.Width, MidpointRounding.AwayFromZero);
        targetOffset = Math.Clamp((currentPage + pageDelta) * workspace.Width, minimumOffset, maximumOffset);
        return Math.Abs(targetOffset - offset) >= AlignmentTolerance;
    }
}

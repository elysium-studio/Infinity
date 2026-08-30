using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;

namespace Infinity.Application;

public sealed class PageScrollSnapTargetResolver(IWorkspace workspace) :
    IScrollSnapTargetResolver
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
}

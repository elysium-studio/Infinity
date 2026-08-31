namespace Infinity.Application.Abstractions;

public interface IPageCenterTargetResolver
{
    bool TryResolve(double offset, double minimumOffset, double maximumOffset, out double targetOffset);

    bool TryResolveAdjacent(double offset, int pageDelta, double minimumOffset, double maximumOffset, out double targetOffset);
}

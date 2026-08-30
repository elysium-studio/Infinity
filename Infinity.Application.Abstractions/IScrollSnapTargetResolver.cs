namespace Infinity.Application.Abstractions;

public interface IScrollSnapTargetResolver
{
    bool TryResolve(double offset, double minimumOffset, double maximumOffset, out double targetOffset);
}

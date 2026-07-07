namespace Infinity.Application.Abstractions;

public interface IWindowFilterCondition
{
    bool IsActive { get; }

    bool ShouldFilter(TrackedWindow trackedWindow);
}
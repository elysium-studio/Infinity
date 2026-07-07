namespace Infinity.Application.Abstractions;

public interface IPeekSource
{
    bool IsActive { get; }

    bool RevealsWindow(TrackedWindow trackedWindow);
}
namespace Infinity.Application.Abstractions;

public interface IWindowSelector
{
    IntPtr SelectedHandle { get; }

    void Select(ITrackedWindow window);

    void Step(bool forward, IReadOnlyCollection<ITrackedWindow> candidates);

    void Clear(IReadOnlyCollection<ITrackedWindow> all);

    IntPtr Resolve(IReadOnlyCollection<ITrackedWindow> all);
}
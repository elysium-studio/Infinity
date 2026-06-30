namespace Infinity.Application.Abstractions;

public interface IWindowSelector
{
    IntPtr SelectedHandle { get; }

    void Select(ITrackedWindow window);

    void Step(bool forward, IEnumerable<ITrackedWindow> candidates);

    void Clear(IEnumerable<ITrackedWindow> items);

    IntPtr Resolve(IEnumerable<ITrackedWindow> items);
}
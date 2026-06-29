using Infinity.Application.Abstractions;

namespace Infinity.Shell;

public interface ITrackedWindowCollection
{
    IReadOnlyList<ITrackedWindow> All { get; }

    void Add(IntPtr handle, ITrackedWindow window);

    void Remove(IntPtr handle);

    bool TryGet(IntPtr handle, out ITrackedWindow? window);

    bool Contains(IntPtr handle);
}
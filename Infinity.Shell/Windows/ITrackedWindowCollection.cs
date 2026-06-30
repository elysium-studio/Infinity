using Infinity.Application.Abstractions;

namespace Infinity.Shell;

public interface ITrackedWindowCollection :
    IEnumerable<ITrackedWindow>
{
    void Add(IntPtr handle, ITrackedWindow window);

    void Remove(IntPtr handle);

    bool TryGet(IntPtr handle, out ITrackedWindow? window);

    bool Contains(IntPtr handle);
}
using Infinity.Application.Abstractions;

namespace Infinity.Shell;

public class TrackedWindowCollection :
    ITrackedWindowCollection
{
    private readonly Dictionary<IntPtr, ITrackedWindow> map = [];

    public IReadOnlyList<ITrackedWindow> All => [.. map.Values];

    public void Add(IntPtr handle, ITrackedWindow window) =>
        map[handle] = window;

    public void Remove(IntPtr handle) =>
        map.Remove(handle);

    public bool TryGet(IntPtr handle, out ITrackedWindow? window) =>
        map.TryGetValue(handle, out window);

    public bool Contains(IntPtr handle) =>
        map.ContainsKey(handle);
}
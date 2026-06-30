using Infinity.Application.Abstractions;
using System.Collections;

namespace Infinity.Shell;

public class TrackedWindowCollection :
    ITrackedWindowCollection
{
    private readonly Dictionary<IntPtr, ITrackedWindow> map = [];
    public void Add(IntPtr handle, ITrackedWindow window) =>
        map[handle] = window;

    public void Remove(IntPtr handle) =>
        map.Remove(handle);

    public bool TryGet(IntPtr handle, out ITrackedWindow? window) =>
        map.TryGetValue(handle, out window);

    public bool Contains(IntPtr handle) => 
        map.ContainsKey(handle);

    public Dictionary<IntPtr, ITrackedWindow>.ValueCollection.Enumerator GetEnumerator() =>
        map.Values.GetEnumerator();

    IEnumerator<ITrackedWindow> IEnumerable<ITrackedWindow>.GetEnumerator() =>
        map.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() =>
        map.Values.GetEnumerator();
}
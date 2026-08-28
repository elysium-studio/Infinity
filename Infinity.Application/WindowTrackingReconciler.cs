using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;

namespace Infinity.Application;

public sealed class WindowTrackingReconciler(
    IWindowStore repository,
    IWindowFilter filter,
    IWindowEnumerator enumerator,
    nint ownerWindowHandle)
{
    public void Reconcile(
        Action<nint, IReadOnlyDictionary<nint, int>> register,
        Action<nint> unregister)
    {
        List<nint> liveWindows = EnumerateTopLevelWindows();
        HashSet<nint> liveWindowSet = [.. liveWindows];
        List<nint> staleHandles = [];

        foreach (TrackedWindow trackedWindow in repository)
        {
            if (!liveWindowSet.Contains(trackedWindow.Handle) ||
                !filter.ShouldTrack(trackedWindow.Handle, ownerWindowHandle))
            {
                staleHandles.Add(trackedWindow.Handle);
            }
        }

        foreach (nint staleHandle in staleHandles)
        {
            unregister(staleHandle);
        }

        IReadOnlyDictionary<nint, int> windowStackIndices = BuildWindowStackIndexMap();

        foreach (nint liveWindow in liveWindows)
        {
            if (!repository.TryGet(liveWindow, out _))
            {
                register(liveWindow, windowStackIndices);
            }
        }
    }

    public void RefreshStackIndices()
    {
        IReadOnlyDictionary<nint, int> windowStackIndices = BuildWindowStackIndexMap();

        foreach (TrackedWindow trackedWindow in repository)
        {
            if (windowStackIndices.TryGetValue(trackedWindow.Handle, out int zIndex))
            {
                trackedWindow.ZIndex = zIndex;
            }
        }
    }

    public int GetZIndex(nint windowHandle)
    {
        IReadOnlyDictionary<nint, int> windowStackIndices = BuildWindowStackIndexMap();
        return windowStackIndices.TryGetValue(windowHandle, out int zIndex) ? zIndex : int.MaxValue;
    }

    private List<nint> EnumerateTopLevelWindows()
    {
        List<nint> windows = [];
        enumerator.EnumerateVisible(windowHandle => windows.Add(windowHandle));
        return windows;
    }

    private Dictionary<nint, int> BuildWindowStackIndexMap()
    {
        Dictionary<nint, int> windowStackIndices = [];
        int index = 0;

        enumerator.EnumerateVisible(windowHandle =>
        {
            windowStackIndices[windowHandle] = index;
            index++;
        });

        return windowStackIndices;
    }
}

using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;

namespace Infinity.Application;

public class WindowSelector(ISelectionPreviewQueue previewQueue,
    IWindowStore store,
    IPager pager,
    IWorkspace workspace) :
    IWindowSelector
{
    private IntPtr selectedHandle;

    public IntPtr SelectedHandle => selectedHandle;

    public void Select(ITrackedWindow window)
    {
        if (selectedHandle == window.Handle && window.IsSelected)
        {
            return;
        }

        window.IsSelected = true;
        selectedHandle = window.Handle;

        previewQueue.Queue(window.Handle, () => selectedHandle);
        NavigateToPage(window.Handle);
    }

    public void Step(bool forward, IReadOnlyCollection<ITrackedWindow> candidates)
    {
        List<ITrackedWindow> ordered = [.. candidates
            .Where(window => !window.IsFiltered)
            .OrderBy(window => window.X)];

        if (ordered.Count == 0)
        {
            Clear(candidates);
            return;
        }

        int currentIndex = ordered.FindIndex(w => w.Handle == selectedHandle);
        int nextIndex;

        if (forward)
        {
            nextIndex = currentIndex >= 0 && currentIndex < ordered.Count - 1 ? currentIndex + 1 : 0;
        }
        else
        {
            nextIndex = currentIndex > 0 ? currentIndex - 1 : ordered.Count - 1;
        }

        ClearSelected(candidates);
        Select(ordered[nextIndex]);
    }

    public void Clear(IReadOnlyCollection<ITrackedWindow> all)
    {
        ClearSelected(all);
        selectedHandle = default;
        previewQueue.Cancel();
    }

    public IntPtr Resolve(IReadOnlyCollection<ITrackedWindow> all)
    {
        if (selectedHandle != default)
        {
            ITrackedWindow? current = all.FirstOrDefault(w => w.Handle == selectedHandle);

            if (current is not null && current.IsSelected)
            {
                return selectedHandle;
            }
        }

        ITrackedWindow? selected = all.FirstOrDefault(w => w.IsSelected);

        if (selected is null)
        {
            selectedHandle = default;
            return default;
        }

        selectedHandle = selected.Handle;
        return selectedHandle;
    }

    private void ClearSelected(IReadOnlyCollection<ITrackedWindow> all)
    {
        foreach (ITrackedWindow window in all)
        {
            if (window.IsSelected)
            {
                window.IsSelected = false;
            }
        }
    }

    private void NavigateToPage(IntPtr handle)
    {
        if (!store.TryGet(handle, out TrackedWindow? trackedWindow))
        {
            return;
        }

        int page = (int)Math.Floor((double)trackedWindow.CanvasX / workspace.Width);

        if (page == pager.CurrentPage)
        {
            return;
        }

        pager.NavigateToPage(page);
    }
}
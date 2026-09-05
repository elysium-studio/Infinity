using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;

namespace Infinity.Shell;

public sealed class DesktopSnapAppearanceCoordinator(IWindowStore windows, IWorkspace workspace, PageLayoutStore layouts, DesktopSnapLayoutCatalog catalog, DesktopSnapPlacementResolver resolver, IWindowSnapAppearance appearance, DesktopWindowFrameGeometry frameGeometry)
{
    private readonly HashSet<nint> slotted = [];
    private readonly HashSet<nint> dragging = [];
    private readonly HashSet<nint> observed = [];
    private readonly HashSet<nint> attempted = [];
    private readonly Lock gate = new();
    private bool started;

    public bool IsSlotted(nint handle)
    {
        lock (gate)
        {
            return slotted.Contains(handle);
        }
    }


    public void Start()
    {
        if (started)
        {
            return;
        }

        started = true;
        windows.WindowAdded += HandleChanged;
        windows.WindowChanged += HandleChanged;
        windows.WindowRemoved += HandleRemoved;
        layouts.LayoutChanged += HandleLayoutChanged;
        workspace.WorkspaceLayoutChanged += HandleWorkspaceChanged;
        RefreshAll();
    }


    public void Stop()
    {
        if (!started)
        {
            return;
        }

        started = false;
        windows.WindowAdded -= HandleChanged;
        windows.WindowChanged -= HandleChanged;
        windows.WindowRemoved -= HandleRemoved;
        layouts.LayoutChanged -= HandleLayoutChanged;
        workspace.WorkspaceLayoutChanged -= HandleWorkspaceChanged;
        lock (gate)
        {
            foreach (nint handle in slotted)
            {
                appearance.Restore(handle);
            }

            slotted.Clear();
            dragging.Clear();
            observed.Clear();
            attempted.Clear();
        }
    }


    public void BeginMove(nint handle)
    {
        lock (gate)
        {
            dragging.Add(handle);
            slotted.Remove(handle);
            attempted.Remove(handle);
            appearance.Restore(handle);
        }
    }


    public void EndMove(nint handle)
    {
        lock (gate)
        {
            dragging.Remove(handle);
        }

        if (windows.TryGet(handle, out TrackedWindow? window))
        {
            Refresh(window);
        }
    }


    public void Refresh(TrackedWindow window)
    {
        lock (gate)
        {
            bool shouldBeSlotted = !dragging.Contains(window.Handle) && MatchesSlot(window);
            bool firstSeen = observed.Add(window.Handle);
            if (shouldBeSlotted)
            {
                if (!slotted.Contains(window.Handle) && attempted.Add(window.Handle) && appearance.TryApply(window.Handle))
                {
                    slotted.Add(window.Handle);
                }
            }
            else
            {
                bool wasSlotted = slotted.Remove(window.Handle);
                attempted.Remove(window.Handle);
                if (wasSlotted || firstSeen)
                {
                    appearance.Restore(window.Handle);
                }
            }
        }
    }


    private bool MatchesSlot(TrackedWindow window)
    {
        if (workspace.Width <= 0 || workspace.Height <= 0)
        {
            return false;
        }

        int page = Math.Max(0, (int)Math.Floor((window.CanvasX - (double)workspace.WorkAreaX + window.Width / 2d) / workspace.Width));
        DesktopSnapLayoutKind layout = layouts.GetLayout(page);
        DesktopSnapLayoutDefinition? definition = catalog.Get(layout);
        if (definition is null)
        {
            return false;
        }

        DesktopSnapPlacement visible = frameGeometry.GetVisiblePlacement(window);
        for (int slot = 0; slot < definition.Slots.Count; slot++)
        {
            if (resolver.TryResolve(page, layout, slot, workspace.WorkAreaX, workspace.WorkAreaY, out DesktopSnapPlacement bounds) && Math.Abs(visible.CanvasX - bounds.CanvasX) <= 2 && Math.Abs(visible.CanvasY - bounds.CanvasY) <= 2 && Math.Abs(visible.Width - bounds.Width) <= 2 && Math.Abs(visible.Height - bounds.Height) <= 2)
            {
                return true;
            }
        }

        return false;
    }


    private void HandleChanged(object? sender, TrackedWindow window) => Refresh(window);

    private void HandleRemoved(object? sender, nint handle)
    {
        lock (gate)
        {
            slotted.Remove(handle);
            dragging.Remove(handle);
            observed.Remove(handle);
            attempted.Remove(handle);
            appearance.Restore(handle);
        }
    }


    private void HandleLayoutChanged(int page, DesktopSnapLayoutKind layout) => RefreshAll();

    private void HandleWorkspaceChanged(object? sender, EventArgs args) => RefreshAll();

    private void RefreshAll()
    {
        foreach (TrackedWindow window in windows)
        {
            Refresh(window);
        }
    }
}

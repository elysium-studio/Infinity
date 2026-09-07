namespace Infinity.Shell;

public sealed class DesktopOverviewRefreshQueue
{
    private readonly Lock gate = new();
    private readonly HashSet<nint> windows = [];
    private readonly Func<Action, bool> enqueue;
    private readonly Action<DesktopOverviewRefreshBatch> refresh;
    private readonly Action drain;
    private bool active;
    private bool queued;
    private bool synchronise;
    private bool layout;

    public DesktopOverviewRefreshQueue(
        Func<Action, bool> enqueue,
        Action<DesktopOverviewRefreshBatch> refresh)
    {
        this.enqueue = enqueue;
        this.refresh = refresh;
        drain = Drain;
    }

    public void Start()
    {
        lock (gate)
        {
            active = true;
        }
    }

    public void Stop()
    {
        lock (gate)
        {
            active = false;
            synchronise = false;
            layout = false;
            windows.Clear();
        }
    }

    public void RequestSynchronise() => Request(true, false, 0);

    public void RequestLayout() => Request(false, true, 0);

    public void RequestWindow(nint handle) => Request(false, false, handle);

    private void Request(bool needsSynchronise, bool needsLayout, nint handle)
    {
        lock (gate)
        {
            if (!active)
            {
                return;
            }

            synchronise |= needsSynchronise;
            layout |= needsLayout;
            if (synchronise)
            {
                windows.Clear();
            }
            else if (handle != 0)
            {
                windows.Add(handle);
            }

            if (!queued)
            {
                queued = true;
                if (!enqueue(drain))
                {
                    queued = false;
                }
            }
        }
    }

    private void Drain()
    {
        DesktopOverviewRefreshBatch batch;
        lock (gate)
        {
            queued = false;
            if (!active || (!synchronise && !layout && windows.Count == 0))
            {
                return;
            }

            batch = new(synchronise, layout, synchronise ? [] : [.. windows]);
            synchronise = false;
            layout = false;
            windows.Clear();
        }

        refresh(batch);
    }
}

public readonly record struct DesktopOverviewRefreshBatch(
    bool Synchronise,
    bool Layout,
    IReadOnlyList<nint> Windows);

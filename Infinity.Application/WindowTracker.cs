using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
namespace Infinity.Application;

public class WindowTracker(IWindowStore repository,
    IWindowGeometryReader reader,
    IWindowFilter filter,
    IWindowAncestorResolver ancestorResolver,
    IWindowRestoreGuard restoreGuard,
    IWindowMoveGuard moveGuard,
    IWindowDragGuard dragGuard,
    IWindowEnumerator enumerator,
    IWindowEventListener listener,
    IPanState state,
    IDispatcher dispatcher,
    IntPtr handle,
    ILogger<WindowTracker> logger) :
    IWindowTracker
{
    private const int SelfHealIntervalMilliseconds = 3000;

    private readonly Dictionary<IntPtr, int> suspendedCanvasPositions = [];

    private Timer? selfHealTimer;
    private int selfHealInProgress;

    public event Action<IntPtr>? WindowRestored;

    public void Start()
    {
        listener.WindowCreated += HandleWindowCreated;
        listener.WindowShown += HandleWindowShown;
        listener.WindowDestroyed += HandleWindowDestroyed;
        listener.MinimizeStarted += HandleMinimizeStarted;
        listener.MinimizeEnded += HandleMinimizeEnded;
        listener.DragEnded += HandleDragEnded;
        listener.WindowLocationChanged += HandleWindowLocationChanged;
        listener.ZOrderChanged += HandleZOrderChanged;
        state.OffsetChanged += HandleOffsetChanged;

        selfHealTimer = new Timer(HandleSelfHealTick, null, SelfHealIntervalMilliseconds, SelfHealIntervalMilliseconds);
    }

    public void Stop()
    {
        listener.WindowCreated -= HandleWindowCreated;
        listener.WindowShown -= HandleWindowShown;
        listener.WindowDestroyed -= HandleWindowDestroyed;
        listener.MinimizeStarted -= HandleMinimizeStarted;
        listener.MinimizeEnded -= HandleMinimizeEnded;
        listener.DragEnded -= HandleDragEnded;
        listener.WindowLocationChanged -= HandleWindowLocationChanged;
        listener.ZOrderChanged -= HandleZOrderChanged;
        state.OffsetChanged -= HandleOffsetChanged;

        selfHealTimer?.Dispose();
        selfHealTimer = null;
    }

    public void TryRegister(IntPtr windowHandle) => TryRegister(windowHandle, null);

    private void TryRegister(IntPtr windowHandle, Dictionary<IntPtr, int>? zIndexMap)
    {
        if (repository.TryGet(windowHandle, out _))
        {
            logger.LogDebug("TryRegister: {Handle} skipped, already tracked", windowHandle);
            return;
        }

        if (!reader.IsVisible(windowHandle))
        {
            logger.LogDebug("TryRegister: {Handle} skipped, not visible", windowHandle);
            return;
        }

        if (reader.IsMinimised(windowHandle))
        {
            logger.LogDebug("TryRegister: {Handle} skipped, minimised", windowHandle);
            return;
        }

        if (!filter.ShouldTrack(windowHandle, handle))
        {
            IntPtr ancestor = ancestorResolver.GetRootAncestor(windowHandle);

            if (ancestor == windowHandle || ancestor == IntPtr.Zero)
            {
                logger.LogDebug("TryRegister: {Handle} skipped, filtered out with no trackable ancestor", windowHandle);
                return;
            }

            logger.LogDebug("TryRegister: {Handle} filtered, redirecting to ancestor {Ancestor}", windowHandle, ancestor);
            TryRegister(ancestor, zIndexMap);
            return;
        }

        if (!reader.TryReadGeometry(windowHandle, out int x, out int y, out int width, out int height))
        {
            logger.LogWarning("TryRegister: {Handle} skipped, geometry read failed", windowHandle);
            return;
        }

        bool isRestore = suspendedCanvasPositions.TryGetValue(windowHandle, out int previousCanvasX);

        if (isRestore)
        {
            restoreGuard.MarkRestoring(windowHandle);
            suspendedCanvasPositions.Remove(windowHandle);
        }

        int canvasX = isRestore ? previousCanvasX : x + (int)Math.Round(state.Offset);
        int lastPlacedX = canvasX - (int)Math.Round(state.Offset);
        int zIndex = zIndexMap is not null && zIndexMap.TryGetValue(windowHandle, out int mappedZIndex) ? mappedZIndex : GetZIndex(windowHandle);

        repository.Add(new TrackedWindow
        {
            Handle = windowHandle,
            CanvasX = canvasX,
            CanvasY = y,
            Width = width,
            Height = height,
            LastPlacedX = lastPlacedX,
            LastPlacedY = y,
            ZIndex = zIndex
        });

        logger.LogInformation(isRestore
            ? "Window restored: {Handle} canvasX={CanvasX} canvasY={CanvasY} screenX={ScreenX} screenY={ScreenY} lastPlacedX={LastPlacedX} width={Width} height={Height} zIndex={ZIndex}"
            : "Window registered: {Handle} canvasX={CanvasX} canvasY={CanvasY} screenX={ScreenX} screenY={ScreenY} lastPlacedX={LastPlacedX} width={Width} height={Height} zIndex={ZIndex}",
            windowHandle, canvasX, y, x, y, lastPlacedX, width, height, zIndex);

        if (isRestore)
        {
            WindowRestored?.Invoke(windowHandle);
        }
    }

    private void HandleWindowCreated(IntPtr windowHandle)
    {
        logger.LogDebug("Event: WindowCreated {Handle}", windowHandle);
        TryRegister(windowHandle);
    }

    private void HandleWindowShown(IntPtr windowHandle)
    {
        logger.LogDebug("Event: WindowShown {Handle}", windowHandle);
        TryRegister(windowHandle);
    }

    private void HandleWindowDestroyed(IntPtr windowHandle)
    {
        logger.LogDebug("Event: WindowDestroyed {Handle}", windowHandle);
        suspendedCanvasPositions.Remove(windowHandle);
        Unregister(windowHandle);
    }

    private void HandleMinimizeStarted(IntPtr windowHandle)
    {
        logger.LogDebug("Event: MinimizeStarted {Handle}", windowHandle);
        SuspendTracking(windowHandle);
    }

    private void HandleMinimizeEnded(IntPtr windowHandle)
    {
        logger.LogDebug("Event: MinimizeEnded {Handle}", windowHandle);
        TryRegister(windowHandle);
    }

    private void HandleDragEnded(IntPtr windowHandle)
    {
        logger.LogDebug("Event: DragEnded {Handle}", windowHandle);
        HandleWindowMovedExternally(windowHandle);
    }

    private void HandleWindowLocationChanged(IntPtr windowHandle)
    {
        if (moveGuard.IsSystemMove)
        {
            logger.LogTrace("Event: WindowLocationChanged {Handle} ignored, system move in progress", windowHandle);
            return;
        }

        logger.LogDebug("Event: WindowLocationChanged {Handle}", windowHandle);
        HandleWindowMovedExternally(windowHandle);
    }

    private void HandleZOrderChanged() => RefreshZIndices();

    private void HandleOffsetChanged()
    {
        IntPtr draggingWindow = dragGuard.DraggingWindow;

        if (draggingWindow == IntPtr.Zero)
        {
            return;
        }

        if (!repository.TryGet(draggingWindow, out TrackedWindow trackedWindow))
        {
            return;
        }

        trackedWindow.CanvasX = trackedWindow.LastPlacedX + (int)Math.Round(state.Offset);
    }

    private void SuspendTracking(IntPtr windowHandle)
    {
        if (!repository.TryGet(windowHandle, out TrackedWindow trackedWindow))
        {
            logger.LogDebug("SuspendTracking: {Handle} skipped, not tracked", windowHandle);
            return;
        }

        suspendedCanvasPositions[windowHandle] = trackedWindow.CanvasX;
        logger.LogInformation("Window suspended: {Handle} savedCanvasX={CanvasX}", windowHandle, trackedWindow.CanvasX);
        Unregister(windowHandle);
    }

    private void HandleWindowMovedExternally(IntPtr windowHandle)
    {
        if (!repository.TryGet(windowHandle, out TrackedWindow trackedWindow))
        {
            logger.LogDebug("MovedExternally: {Handle} skipped, not tracked", windowHandle);
            return;
        }

        if (restoreGuard.IsRestoring(windowHandle))
        {
            logger.LogDebug("MovedExternally: {Handle} skipped, restore in progress", windowHandle);
            return;
        }

        if (!reader.IsVisible(windowHandle) || reader.IsMinimised(windowHandle))
        {
            logger.LogDebug("MovedExternally: {Handle} skipped, not visible/minimised (geometry ignored to avoid corrupting CanvasX/CanvasY)", windowHandle);
            return;
        }

        if (!reader.TryReadGeometry(windowHandle, out int x, out int y, out int width, out int height))
        {
            logger.LogWarning("MovedExternally: {Handle} skipped, geometry read failed", windowHandle);
            return;
        }

        if (x == trackedWindow.LastPlacedX && y == trackedWindow.LastPlacedY)
        {
            return;
        }

        int newCanvasX = x + (int)Math.Round(state.Offset);

        logger.LogInformation("MovedExternally: {Handle} oldScreenX={OldX} oldScreenY={OldY} newScreenX={NewX} newScreenY={NewY} oldCanvasX={OldCanvasX} newCanvasX={NewCanvasX}",
            windowHandle, trackedWindow.LastPlacedX, trackedWindow.LastPlacedY, x, y, trackedWindow.CanvasX, newCanvasX);

        trackedWindow.CanvasX = newCanvasX;
        trackedWindow.CanvasY = y;
        trackedWindow.Width = width;
        trackedWindow.Height = height;
        trackedWindow.LastPlacedX = x;
        trackedWindow.LastPlacedY = y;
    }

    private void Unregister(IntPtr windowHandle)
    {
        logger.LogInformation("Window unregistered: {Handle}", windowHandle);
        repository.Remove(windowHandle);
    }

    private void RefreshZIndices()
    {
        Dictionary<IntPtr, int> zIndexMap = BuildZIndexMap();

        foreach (TrackedWindow trackedWindow in repository)
        {
            if (zIndexMap.TryGetValue(trackedWindow.Handle, out int zIndex))
            {
                trackedWindow.ZIndex = zIndex;
            }
        }
    }

    private void HandleSelfHealTick(object? timerState)
    {
        if (Interlocked.CompareExchange(ref selfHealInProgress, 1, 0) != 0)
        {
            return;
        }

        dispatcher.Dispatch(RunSelfHeal);
    }

    private void RunSelfHeal()
    {
        try
        {
            logger.LogDebug("Self-heal sweep starting, tracked count={TrackedCount}", repository.Count);

            List<IntPtr> liveWindows = EnumerateTopLevelWindows();
            HashSet<IntPtr> liveWindowSet = [.. liveWindows];
            List<IntPtr> staleHandles = [];

            foreach (TrackedWindow trackedWindow in repository)
            {
                if (!liveWindowSet.Contains(trackedWindow.Handle))
                {
                    staleHandles.Add(trackedWindow.Handle);
                }
            }

            foreach (IntPtr staleHandle in staleHandles)
            {
                logger.LogWarning("Self-heal: removing stale tracked window no longer present: {Handle}", staleHandle);
                suspendedCanvasPositions.Remove(staleHandle);
                Unregister(staleHandle);
            }

            int countBeforeRecovery = repository.Count;
            Dictionary<IntPtr, int> zIndexMap = BuildZIndexMap();

            foreach (IntPtr liveWindow in liveWindows)
            {
                if (!repository.TryGet(liveWindow, out _))
                {
                    logger.LogDebug("Self-heal: attempting to register untracked live window: {Handle}", liveWindow);
                    TryRegister(liveWindow, zIndexMap);
                }
            }

            int recoveredCount = repository.Count - countBeforeRecovery;

            if (recoveredCount > 0)
            {
                logger.LogWarning("Self-heal: recovered {Count} windows that fell out of tracking", recoveredCount);
            }

            logger.LogDebug("Self-heal sweep finished, tracked count={TrackedCount}", repository.Count);
        }
        finally
        {
            Volatile.Write(ref selfHealInProgress, 0);
        }
    }

    private List<IntPtr> EnumerateTopLevelWindows()
    {
        List<IntPtr> windows = [];

        enumerator.EnumerateVisible(windowHandle => windows.Add(windowHandle));

        return windows;
    }

    private Dictionary<IntPtr, int> BuildZIndexMap()
    {
        Dictionary<IntPtr, int> zIndexMap = [];
        int index = 0;

        enumerator.EnumerateVisible(windowHandle =>
        {
            zIndexMap[windowHandle] = index;
            index++;
        });

        return zIndexMap;
    }

    private int GetZIndex(IntPtr windowHandle)
    {
        Dictionary<IntPtr, int> zIndexMap = BuildZIndexMap();

        return zIndexMap.TryGetValue(windowHandle, out int zIndex) ? zIndex : int.MaxValue;
    }
}
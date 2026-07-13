using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;

namespace Infinity.Application;

public class WindowTracker(IWindowStore repository,
    IWindowGeometryReader reader,
    IWindowFilter filter,
    IWindowAncestorResolver ancestorResolver,
    IWindowRestoreGuard restoreGuard,
    IWindowPlacementRules placementRules,
    IWindowMoveGuard moveGuard,
    IWindowMover mover,
    IWindowConcealer concealer,
    IWindowDragGuard dragGuard,
    IWindowEnumerator enumerator,
    IWindowEventListener listener,
    IWorkspace workspace,
    IPager pager,
    IPanState state,
    IDispatcher dispatcher,
    IntPtr handle) :
    IWindowTracker
{
    private const int SelfHealIntervalMilliseconds = 3000;

    private static readonly TimeSpan MinimizeSuspendDelay = TimeSpan.FromMilliseconds(160);

    private readonly Dictionary<IntPtr, SuspendedWindowState> suspendedWindowStates = [];
    private readonly Dictionary<IntPtr, CancellationTokenSource> pendingMinimizeSuspensions = [];
    private readonly HashSet<IntPtr> pendingNewWindows = [];
    private readonly Lock minimizeSyncRoot = new();
    private readonly Lock registrationSyncRoot = new();

    private Timer? selfHealTimer;
    private int selfHealInProgress;

    public void Start()
    {
        listener.WindowCreated += HandleWindowCreated;
        listener.WindowShown += HandleWindowShown;
        listener.WindowDestroyed += HandleWindowDestroyed;
        listener.MinimizeStarted += HandleMinimizeStarted;
        listener.MinimizeEnded += HandleMinimizeEnded;
        listener.DragEnded += HandleDragEnded;
        listener.WindowLocationChanged += HandleWindowLocationChanged;
        listener.WindowStackChanged += HandleWindowStackChanged;
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
        listener.WindowStackChanged -= HandleWindowStackChanged;
        state.OffsetChanged -= HandleOffsetChanged;

        CancelPendingMinimizeSuspensions();

        lock (registrationSyncRoot)
        {
            pendingNewWindows.Clear();
        }

        selfHealTimer?.Dispose();
        selfHealTimer = null;
    }

    public void TryRegisterExisting(IntPtr windowHandle) => TryRegister(windowHandle, null, false);

    private void TryRegister(IntPtr windowHandle,
        Dictionary<IntPtr, int>? windowStackIndexMap,
        bool applyPlacementRule)
    {
        if (repository.TryGet(windowHandle, out _))
        {
            return;
        }

        if (!reader.IsVisible(windowHandle))
        {
            return;
        }

        if (reader.IsMinimised(windowHandle))
        {
            return;
        }

        if (!filter.ShouldTrack(windowHandle, handle))
        {
            IntPtr ancestor = ancestorResolver.GetRootAncestor(windowHandle);

            if (ancestor == windowHandle || ancestor == IntPtr.Zero)
            {
                return;
            }

            TryRegister(ancestor, windowStackIndexMap, applyPlacementRule);
            return;
        }

        if (!reader.TryReadGeometry(windowHandle, out int x, out int y, out int width, out int height))
        {
            return;
        }

        bool isRestore = suspendedWindowStates.TryGetValue(windowHandle, out SuspendedWindowState suspendedState);

        if (isRestore)
        {
            restoreGuard.MarkRestoring(windowHandle);
            suspendedWindowStates.Remove(windowHandle);
        }

        int currentOffset = (int)Math.Round(state.Offset);
        long restoredStickyCanvasX = (long)currentOffset + suspendedState.StickyViewportX;
        bool restoreSticky = isRestore &&
            suspendedState.IsSticky &&
            restoredStickyCanvasX is >= int.MinValue and <= int.MaxValue;
        int canvasX = isRestore
            ? restoreSticky
                ? (int)restoredStickyCanvasX
                : suspendedState.CanvasX
            : x + currentOffset;
        bool placementRuleApplied = applyPlacementRule && !isRestore && TryApplyPlacementRule(windowHandle, ref canvasX);
        int lastPlacedX = canvasX - currentOffset;
        int zIndex = windowStackIndexMap is not null && windowStackIndexMap.TryGetValue(windowHandle, out int mappedZIndex) ? mappedZIndex : GetZIndex(windowHandle);

        TrackedWindow trackedWindow = new()
        {
            Handle = windowHandle,
            CanvasX = canvasX,
            CanvasY = y,
            Width = width,
            Height = height,
            LastPlacedX = lastPlacedX,
            LastPlacedY = y,
            ZIndex = zIndex,
            IsSticky = restoreSticky,
            StickyViewportX = restoreSticky ? suspendedState.StickyViewportX : 0
        };

        repository.Add(trackedWindow);
        ForgetPendingNewWindow(windowHandle);

        if (placementRuleApplied)
        {
            MoveWindowToTrackedPosition(trackedWindow);
        }
    }

    private void HandleWindowCreated(IntPtr windowHandle)
    {
        MarkPendingNewWindow(windowHandle);
        TryRegister(windowHandle, null, true);
    }

    private void HandleWindowShown(IntPtr windowHandle) =>
        TryRegister(windowHandle, null, IsPendingNewWindow(windowHandle));

    private void HandleWindowDestroyed(IntPtr windowHandle)
    {
        CancelPendingMinimizeSuspension(windowHandle);
        suspendedWindowStates.Remove(windowHandle);
        ForgetPendingNewWindow(windowHandle);
        Unregister(windowHandle);
    }

    private void HandleMinimizeStarted(IntPtr windowHandle) => QueueMinimizeSuspension(windowHandle);

    private void HandleMinimizeEnded(IntPtr windowHandle)
    {
        CancelPendingMinimizeSuspension(windowHandle);
        TryRegister(windowHandle, null, IsPendingNewWindow(windowHandle));
    }

    private void HandleDragEnded(IntPtr windowHandle) => HandleWindowMovedExternally(windowHandle);

    private void HandleWindowLocationChanged(IntPtr windowHandle)
    {
        if (moveGuard.IsSystemMove && windowHandle != dragGuard.DraggingWindow)
        {
            return;
        }

        HandleWindowMovedExternally(windowHandle);
    }

    private void HandleWindowStackChanged() => RefreshWindowStackIndices();

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

        if (trackedWindow.IsSticky)
        {
            trackedWindow.StickyViewportX = trackedWindow.LastPlacedX;
        }
    }

    private void QueueMinimizeSuspension(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return;
        }

        CancellationTokenSource cancellationTokenSource = new();

        lock (minimizeSyncRoot)
        {
            if (pendingMinimizeSuspensions.Remove(windowHandle, out CancellationTokenSource? existingCancellationTokenSource))
            {
                TryCancel(existingCancellationTokenSource);
                existingCancellationTokenSource.Dispose();
            }

            pendingMinimizeSuspensions[windowHandle] = cancellationTokenSource;
        }

        _ = DelayAndSuspendIfStillMinimizedAsync(windowHandle, cancellationTokenSource);
    }

    private async Task DelayAndSuspendIfStillMinimizedAsync(IntPtr windowHandle, CancellationTokenSource cancellationTokenSource)
    {
        try
        {
            await Task.Delay(MinimizeSuspendDelay, cancellationTokenSource.Token).ConfigureAwait(false);
            dispatcher.Dispatch(() => CommitMinimizeSuspension(windowHandle, cancellationTokenSource));
        }
        catch (OperationCanceledException)
        {
            CleanupMinimizeSuspension(windowHandle, cancellationTokenSource);
        }
        catch (ObjectDisposedException)
        {
            CleanupMinimizeSuspension(windowHandle, cancellationTokenSource);
        }
        catch (InvalidOperationException)
        {
            CleanupMinimizeSuspension(windowHandle, cancellationTokenSource);
        }
    }

    private void CommitMinimizeSuspension(IntPtr windowHandle, CancellationTokenSource cancellationTokenSource)
    {
        if (!TryClaimPendingMinimizeSuspension(windowHandle, cancellationTokenSource))
        {
            return;
        }

        if (!repository.TryGet(windowHandle, out _))
        {
            return;
        }

        if (!reader.IsMinimised(windowHandle))
        {
            return;
        }

        SuspendTracking(windowHandle);
    }

    private bool TryClaimPendingMinimizeSuspension(IntPtr windowHandle, CancellationTokenSource cancellationTokenSource)
    {
        lock (minimizeSyncRoot)
        {
            if (!pendingMinimizeSuspensions.TryGetValue(windowHandle, out CancellationTokenSource? currentCancellationTokenSource))
            {
                return false;
            }

            if (!ReferenceEquals(currentCancellationTokenSource, cancellationTokenSource))
            {
                return false;
            }

            pendingMinimizeSuspensions.Remove(windowHandle);
        }

        cancellationTokenSource.Dispose();
        return true;
    }

    private void CleanupMinimizeSuspension(IntPtr windowHandle, CancellationTokenSource cancellationTokenSource)
    {
        lock (minimizeSyncRoot)
        {
            if (pendingMinimizeSuspensions.TryGetValue(windowHandle, out CancellationTokenSource? currentCancellationTokenSource) &&
                ReferenceEquals(currentCancellationTokenSource, cancellationTokenSource))
            {
                pendingMinimizeSuspensions.Remove(windowHandle);
            }
        }

        cancellationTokenSource.Dispose();
    }

    private void CancelPendingMinimizeSuspension(IntPtr windowHandle)
    {
        CancellationTokenSource? cancellationTokenSource = null;

        lock (minimizeSyncRoot)
        {
            if (pendingMinimizeSuspensions.Remove(windowHandle, out CancellationTokenSource? removedCancellationTokenSource))
            {
                cancellationTokenSource = removedCancellationTokenSource;
            }
        }

        if (cancellationTokenSource is null)
        {
            return;
        }

        TryCancel(cancellationTokenSource);
        cancellationTokenSource.Dispose();
    }

    private void CancelPendingMinimizeSuspensions()
    {
        List<CancellationTokenSource> cancellationTokenSources;

        lock (minimizeSyncRoot)
        {
            cancellationTokenSources = [.. pendingMinimizeSuspensions.Values];
            pendingMinimizeSuspensions.Clear();
        }

        foreach (CancellationTokenSource cancellationTokenSource in cancellationTokenSources)
        {
            TryCancel(cancellationTokenSource);
            cancellationTokenSource.Dispose();
        }
    }

    private void SuspendTracking(IntPtr windowHandle)
    {
        if (!repository.TryGet(windowHandle, out TrackedWindow trackedWindow))
        {
            return;
        }

        suspendedWindowStates[windowHandle] = new(trackedWindow.CanvasX,
            trackedWindow.IsSticky,
            trackedWindow.StickyViewportX);
        Unregister(windowHandle);
    }

    private void HandleWindowMovedExternally(IntPtr windowHandle)
    {
        if (concealer.IsConcealed(windowHandle))
        {
            return;
        }

        if (!repository.TryGet(windowHandle, out TrackedWindow trackedWindow))
        {
            return;
        }

        if (restoreGuard.IsRestoring(windowHandle))
        {
            return;
        }

        if (!reader.IsVisible(windowHandle) || reader.IsMinimised(windowHandle))
        {
            return;
        }

        if (!reader.TryReadGeometry(windowHandle, out int x, out int y, out int width, out int height))
        {
            return;
        }

        if (x == trackedWindow.LastPlacedX && y == trackedWindow.LastPlacedY)
        {
            return;
        }

        int newCanvasX = x + (int)Math.Round(state.Offset);

        trackedWindow.CanvasX = newCanvasX;
        trackedWindow.CanvasY = y;
        trackedWindow.Width = width;
        trackedWindow.Height = height;
        trackedWindow.LastPlacedX = x;
        trackedWindow.LastPlacedY = y;

        if (trackedWindow.IsSticky)
        {
            trackedWindow.StickyViewportX = x;
        }
    }

    private void Unregister(IntPtr windowHandle) => repository.Remove(windowHandle);

    private void RefreshWindowStackIndices()
    {
        Dictionary<IntPtr, int> windowStackIndexMap = BuildWindowStackIndexMap();

        foreach (TrackedWindow trackedWindow in repository)
        {
            if (windowStackIndexMap.TryGetValue(trackedWindow.Handle, out int zIndex))
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
                suspendedWindowStates.Remove(staleHandle);
                CancelPendingMinimizeSuspension(staleHandle);
                Unregister(staleHandle);
            }

            int countBeforeRecovery = repository.Count;
            Dictionary<IntPtr, int> windowStackIndexMap = BuildWindowStackIndexMap();

            foreach (IntPtr liveWindow in liveWindows)
            {
                if (!repository.TryGet(liveWindow, out _))
                {
                    TryRegister(liveWindow, windowStackIndexMap, IsPendingNewWindow(liveWindow));
                }
            }
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

    private Dictionary<IntPtr, int> BuildWindowStackIndexMap()
    {
        Dictionary<IntPtr, int> windowStackIndexMap = [];
        int index = 0;

        enumerator.EnumerateVisible(windowHandle =>
        {
            windowStackIndexMap[windowHandle] = index;
            index++;
        });

        return windowStackIndexMap;
    }

    private int GetZIndex(IntPtr windowHandle)
    {
        Dictionary<IntPtr, int> windowStackIndexMap = BuildWindowStackIndexMap();

        return windowStackIndexMap.TryGetValue(windowHandle, out int zIndex) ? zIndex : int.MaxValue;
    }

    private bool TryApplyPlacementRule(IntPtr windowHandle, ref int canvasX)
    {
        if (!placementRules.TryGetTargetPage(windowHandle, out int targetPage) || workspace.Width <= 0)
        {
            return false;
        }

        targetPage = Math.Max(0, targetPage);

        if (pager.MaxPages is int maxPages)
        {
            if (maxPages <= 0)
            {
                return false;
            }

            targetPage = Math.Min(targetPage, maxPages - 1);
        }

        int currentPage = (int)Math.Floor(canvasX / (double)workspace.Width);
        long positionWithinPage = (long)canvasX - (long)currentPage * workspace.Width;
        long targetCanvasX = (long)targetPage * workspace.Width + positionWithinPage;

        if (targetCanvasX is < int.MinValue or > int.MaxValue)
        {
            return false;
        }

        canvasX = (int)targetCanvasX;
        return currentPage != targetPage;
    }

    private void MoveWindowToTrackedPosition(TrackedWindow trackedWindow)
    {
        using WindowMoveScope scope = moveGuard.Begin();
        mover.BeginBatch(1);
        mover.MoveTo(trackedWindow.Handle,
            trackedWindow.LastPlacedX,
            trackedWindow.LastPlacedY,
            trackedWindow.Width,
            trackedWindow.Height);
        mover.EndBatch();
    }

    private void MarkPendingNewWindow(IntPtr windowHandle)
    {
        lock (registrationSyncRoot)
        {
            pendingNewWindows.Add(windowHandle);
        }
    }

    private bool IsPendingNewWindow(IntPtr windowHandle)
    {
        lock (registrationSyncRoot)
        {
            return pendingNewWindows.Contains(windowHandle);
        }
    }

    private void ForgetPendingNewWindow(IntPtr windowHandle)
    {
        lock (registrationSyncRoot)
        {
            pendingNewWindows.Remove(windowHandle);
        }
    }

    private static void TryCancel(CancellationTokenSource cancellationTokenSource)
    {
        try
        {
            cancellationTokenSource.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private readonly record struct SuspendedWindowState(int CanvasX,
        bool IsSticky,
        int StickyViewportX);
}

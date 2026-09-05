using System.Runtime.InteropServices;
using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;

namespace Infinity.Application;

public sealed class WindowTracker(IWindowStore repository, IWindowGeometryReader reader, IWindowFilter filter, IWindowAncestorResolver ancestorResolver, IWindowRestoreGuard restoreGuard, IWindowPageTransitionGuard pageTransitionGuard, IWindowMoveGuard moveGuard, IWindowConcealer concealer, IWindowDragGuard dragGuard, ITrackedWindowDragController trackedWindowDragController, WindowTrackingReconciler reconciler, IWindowEventListener listener, IPanState state, IDispatcher dispatcher, ILogger<WindowTracker> logger, IntPtr handle) : IWindowTracker
{
    private const int SelfHealIntervalMilliseconds = 3000;
    private static readonly TimeSpan MinimizeSuspendDelay = TimeSpan.FromMilliseconds(160);
    private readonly Dictionary<IntPtr, SuspendedWindowState> suspendedWindowStates = [];
    private readonly Dictionary<IntPtr, CancellationTokenSource> pendingMinimizeSuspensions = [];
    private readonly Lock minimizeSyncRoot = new();
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
        selfHealTimer = new(HandleSelfHealTick, null, SelfHealIntervalMilliseconds, SelfHealIntervalMilliseconds);
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
        selfHealTimer?.Dispose();
        selfHealTimer = null;
    }


    public void TryRegisterExisting(IntPtr windowHandle) => TryRegister(windowHandle, null);

    private void TryRegister(IntPtr windowHandle, IReadOnlyDictionary<IntPtr, int>? windowStackIndexMap)
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

            TryRegister(ancestor, windowStackIndexMap);
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
        int canvasX = isRestore ? suspendedState.CanvasX : x + currentOffset;
        int lastPlacedX = canvasX - currentOffset;
        int zIndex = windowStackIndexMap is not null && windowStackIndexMap.TryGetValue(windowHandle, out int mappedZIndex) ? mappedZIndex : reconciler.GetZIndex(windowHandle);
        TrackedWindow trackedWindow = new()
        {
            Handle = windowHandle,
            CanvasX = canvasX,
            CanvasY = y,
            Width = width,
            Height = height,
            LastPlacedX = lastPlacedX,
            LastPlacedY = y,
            ZIndex = zIndex
        };
        repository.Add(trackedWindow);
    }


    private void HandleWindowCreated(IntPtr windowHandle) => TryRegister(windowHandle, null);

    private void HandleWindowShown(IntPtr windowHandle) => TryRegister(windowHandle, null);

    private void HandleWindowDestroyed(IntPtr windowHandle)
    {
        CancelPendingMinimizeSuspension(windowHandle);
        suspendedWindowStates.Remove(windowHandle);
        pageTransitionGuard.Clear(windowHandle);
        Unregister(windowHandle);
    }


    private void HandleMinimizeStarted(IntPtr windowHandle) => QueueMinimizeSuspension(windowHandle);

    private void HandleMinimizeEnded(IntPtr windowHandle)
    {
        CancelPendingMinimizeSuspension(windowHandle);
        TryRegister(windowHandle, null);
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


    private void HandleWindowStackChanged() => reconciler.RefreshStackIndices();

    private void HandleOffsetChanged()
    {
        IntPtr draggingWindow = dragGuard.DraggingWindow;
        if (draggingWindow == IntPtr.Zero)
        {
            draggingWindow = trackedWindowDragController.DraggingWindow;
        }

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
        catch (COMException)
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
            if (pendingMinimizeSuspensions.TryGetValue(windowHandle, out CancellationTokenSource? currentCancellationTokenSource) && ReferenceEquals(currentCancellationTokenSource, cancellationTokenSource))
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
            cancellationTokenSources = [..pendingMinimizeSuspensions.Values];
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

        suspendedWindowStates[windowHandle] = new(trackedWindow.CanvasX);
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

        if (x == trackedWindow.LastPlacedX && y == trackedWindow.LastPlacedY && width == trackedWindow.Width && height == trackedWindow.Height)
        {
            return;
        }

        int newCanvasX = x + (int)Math.Round(state.Offset);
        _ = pageTransitionGuard.TryMapToPreservedPage(windowHandle, newCanvasX, width, out newCanvasX);
        trackedWindow.CanvasX = newCanvasX;
        trackedWindow.CanvasY = y;
        trackedWindow.Width = width;
        trackedWindow.Height = height;
        trackedWindow.LastPlacedX = x;
        trackedWindow.LastPlacedY = y;
        repository.NotifyChanged(windowHandle);
    }


    private void Unregister(IntPtr windowHandle) => repository.Remove(windowHandle);

    private void HandleSelfHealTick(object? timerState)
    {
        if (Interlocked.CompareExchange(ref selfHealInProgress, 1, 0) != 0)
        {
            return;
        }

        try
        {
            dispatcher.Dispatch(RunSelfHeal);
        }
        catch (Exception exception)when (exception is InvalidOperationException or COMException)
        {
            Volatile.Write(ref selfHealInProgress, 0);
            logger.LogDebug(exception, "The dispatcher rejected window tracker self-healing");
        }
    }


    private void RunSelfHeal()
    {
        try
        {
            reconciler.Reconcile(TryRegister, RemoveStaleWindow);
        }
        finally
        {
            Volatile.Write(ref selfHealInProgress, 0);
        }
    }


    private void RemoveStaleWindow(IntPtr windowHandle)
    {
        suspendedWindowStates.Remove(windowHandle);
        CancelPendingMinimizeSuspension(windowHandle);
        pageTransitionGuard.Clear(windowHandle);
        Unregister(windowHandle);
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


    private readonly record struct SuspendedWindowState(int CanvasX);
}

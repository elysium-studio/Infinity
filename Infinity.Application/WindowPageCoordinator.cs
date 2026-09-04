using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Infinity.Application;

public sealed class WindowPageCoordinator(IWindowStore store,
    IScroller scroller,
    IWorkspace workspace,
    IWindowActivator activator,
    IDispatcher dispatcher,
    WindowPageGeometry geometry) :
    IWindowNavigationCoordinator,
    IForegroundWindowCoordinator,
    ITrackedForegroundWindowSource,
    ITrackedForegroundWindowTarget
{
    private static readonly TimeSpan ProgrammaticForegroundWindow = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan ForegroundFollowDeferDelay = TimeSpan.FromMilliseconds(80);
    private static readonly TimeSpan ForegroundFollowSuppressionWindow = TimeSpan.FromMilliseconds(900);

    private readonly Lock syncRoot = new();

    private CancellationTokenSource? foregroundFollowCancellationTokenSource;
    private IntPtr expectedProgrammaticHandle;
    private IntPtr foregroundWindowHandle;
    private IntPtr trackedForegroundWindowHandle;
    private IntPtr suppressedForegroundWindowHandle;
    private long expectedProgrammaticAtTimestamp;
    private long foregroundFollowSuppressedAtTimestamp;
    private long foregroundFollowGeneration;
    private bool suppressAllForegroundFollow;

    private int navigationTargetPage = -1;
    private double navigationTargetOffset = -1;
    private IntPtr pendingActivation;

    public event EventHandler<NavigationStartedEventArgs>? NavigationStarted;

    public event EventHandler? NavigationCompleted;

    public event EventHandler? WindowActivationRequested;

    public int NavigationTargetPage
    {
        get
        {
            lock (syncRoot)
            {
                return navigationTargetPage;
            }
        }
        set
        {
            lock (syncRoot)
            {
                navigationTargetPage = value;
            }
        }
    }

    public double NavigationTargetOffset
    {
        get
        {
            lock (syncRoot)
            {
                return navigationTargetOffset;
            }
        }
        set
        {
            lock (syncRoot)
            {
                navigationTargetOffset = value;
            }
        }
    }

    public IntPtr PendingActivation
    {
        get
        {
            lock (syncRoot)
            {
                return pendingActivation;
            }
        }
        set
        {
            lock (syncRoot)
            {
                pendingActivation = value;
            }
        }
    }

    public void NavigateTo(IntPtr handle)
    {
        if (handle == default)
        {
            return;
        }

        if (!store.TryGet(handle, out TrackedWindow? trackedWindow))
        {
            return;
        }

        if (!TryGetWorkspaceWidth(out int workspaceWidth))
        {
            RequestActivation(handle);
            return;
        }

        int windowPage = geometry.GetCenterPage(trackedWindow, workspaceWidth);
        double targetOffset = windowPage * (double)workspaceWidth;

        if (geometry.AreClose(scroller.VisualOffset, targetOffset))
        {
            RequestActivation(handle);
            return;
        }

        PendingActivation = handle;
        SetNavigationTarget(windowPage, targetOffset);
        NavigationStarted?.Invoke(this, new NavigationStartedEventArgs(windowPage));
        scroller.ScrollTo(targetOffset);
    }

    public void NavigateToPage(IntPtr handle)
    {
        if (handle == default)
        {
            return;
        }

        if (!store.TryGet(handle, out TrackedWindow? trackedWindow))
        {
            return;
        }

        if (!TryGetWorkspaceWidth(out int workspaceWidth))
        {
            return;
        }

        if (geometry.IsFullyVisible(trackedWindow, scroller.VisualOffset, workspaceWidth))
        {
            return;
        }

        int windowPage = geometry.GetPage(trackedWindow, workspaceWidth);
        double targetOffset = geometry.GetTargetOffset(trackedWindow, workspaceWidth, windowPage);

        if (IsNavigationSettled(windowPage, targetOffset))
        {
            return;
        }

        SetNavigationTarget(windowPage, targetOffset);
        scroller.ScrollTo(targetOffset);
    }

    public void HandleForegroundWindowChanged(IntPtr handle)
    {
        if (handle == default)
        {
            return;
        }

        if (ShouldIgnoreForegroundWindowChanged(handle))
        {
            return;
        }

        if (!store.TryGet(handle, out TrackedWindow? trackedWindow))
        {
            RecordForegroundWindow(handle, false);
            return;
        }

        RecordForegroundWindow(handle, true);

        if (!TryGetWorkspaceWidth(out int workspaceWidth))
        {
            return;
        }

        if (geometry.IsMeaningfullyVisible(trackedWindow, scroller.VisualOffset, workspaceWidth))
        {
            return;
        }

        QueueForegroundFollow(handle);
    }

    public void HandleWindowMinimizeStarted(IntPtr handle)
    {
        if (handle == default)
        {
            return;
        }

        SuppressAllForegroundFollow();

        lock (syncRoot)
        {
            if (handle == foregroundWindowHandle)
            {
                foregroundWindowHandle = default;
            }

            if (handle == trackedForegroundWindowHandle)
            {
                trackedForegroundWindowHandle = default;
            }
        }
    }

    public void HandleWindowMinimizeEnded(IntPtr handle)
    {
        if (handle == default)
        {
            return;
        }

        SuppressAllForegroundFollow();

        lock (syncRoot)
        {
            foregroundWindowHandle = handle;
            trackedForegroundWindowHandle = handle;
        }

        NavigateToPage(handle);
    }

    public void NotifyWindowClosed(IntPtr handle)
    {
        lock (syncRoot)
        {
            StartForegroundFollowSuppressionCore(true);

            if (handle != default && handle == expectedProgrammaticHandle)
            {
                ClearExpectedProgrammaticActivationCore();
            }

            if (handle != default && handle == foregroundWindowHandle)
            {
                foregroundWindowHandle = default;
            }

            if (handle != default && handle == trackedForegroundWindowHandle)
            {
                trackedForegroundWindowHandle = default;
            }
        }
    }

    public void ExpectProgrammaticActivation(IntPtr handle)
    {
        lock (syncRoot)
        {
            CancelPendingForegroundFollowCore();

            if (handle == default)
            {
                ClearExpectedProgrammaticActivationCore();
                return;
            }

            expectedProgrammaticHandle = handle;
            foregroundWindowHandle = handle;
            trackedForegroundWindowHandle = handle;
            expectedProgrammaticAtTimestamp = Stopwatch.GetTimestamp();
        }
    }

    public void Activate(IntPtr handle)
    {
        if (handle == default)
        {
            return;
        }

        ExpectProgrammaticActivation(handle);
        activator.Activate(handle);
    }

    public void CancelNavigation()
    {
        lock (syncRoot)
        {
            navigationTargetPage = -1;
            navigationTargetOffset = -1;
            pendingActivation = default;
        }
    }

    public void CompleteNavigation()
    {
        IntPtr handle;

        lock (syncRoot)
        {
            if (navigationTargetPage < 0 || !geometry.AreClose(scroller.VisualOffset, navigationTargetOffset))
            {
                return;
            }

            navigationTargetPage = -1;
            navigationTargetOffset = -1;
            handle = pendingActivation;
            pendingActivation = default;
        }

        NavigationCompleted?.Invoke(this, EventArgs.Empty);

        if (handle != default)
        {
            RequestActivation(handle);
        }
    }

    private void RequestActivation(IntPtr handle)
    {
        WindowActivationRequested?.Invoke(this, EventArgs.Empty);
        ExpectProgrammaticActivation(handle);
        activator.Activate(handle);
    }

    private void QueueForegroundFollow(IntPtr handle)
    {
        CancellationTokenSource cancellationTokenSource = new();
        long generation;

        lock (syncRoot)
        {
            CancelPendingForegroundFollowCore();

            foregroundFollowGeneration++;
            generation = foregroundFollowGeneration;
            foregroundFollowCancellationTokenSource = cancellationTokenSource;
        }

        _ = DelayAndCommitForegroundFollowAsync(generation, handle, cancellationTokenSource);
    }

    private async Task DelayAndCommitForegroundFollowAsync(long generation, IntPtr handle, CancellationTokenSource cancellationTokenSource)
    {
        try
        {
            await Task.Delay(ForegroundFollowDeferDelay, cancellationTokenSource.Token).ConfigureAwait(false);
            dispatcher.Dispatch(() => RunCommitForegroundFollow(generation, handle));
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        catch (COMException)
        {
        }
        finally
        {
            CleanupForegroundFollowDelay(generation, cancellationTokenSource);
        }
    }

    private void RunCommitForegroundFollow(long generation, IntPtr handle)
    {
        try
        {
            CommitForegroundFollow(generation, handle);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        catch (COMException)
        {
        }
    }

    private void CommitForegroundFollow(long generation, IntPtr handle)
    {
        if (handle == default)
        {
            return;
        }

        if (!ShouldCommitForegroundFollow(generation, handle))
        {
            return;
        }

        if (!store.TryGet(handle, out TrackedWindow? trackedWindow))
        {
            return;
        }

        if (!TryGetWorkspaceWidth(out int workspaceWidth))
        {
            return;
        }

        if (geometry.IsMeaningfullyVisible(trackedWindow, scroller.VisualOffset, workspaceWidth))
        {
            return;
        }

        int windowPage = geometry.GetPage(trackedWindow, workspaceWidth);
        double targetOffset = geometry.GetTargetOffset(trackedWindow, workspaceWidth, windowPage);

        PendingActivation = handle;

        if (IsNavigationSettled(windowPage, targetOffset))
        {
            RequestActivation(handle);
            return;
        }

        SetNavigationTarget(windowPage, targetOffset);
        scroller.ScrollTo(targetOffset);
    }

    private bool ShouldCommitForegroundFollow(long generation, IntPtr handle)
    {
        lock (syncRoot)
        {
            if (generation != foregroundFollowGeneration)
            {
                return false;
            }

            if (IsForegroundFollowSuppressedCore(handle))
            {
                return false;
            }

            return true;
        }
    }

    private bool ShouldIgnoreForegroundWindowChanged(IntPtr handle)
    {
        lock (syncRoot)
        {
            if (IsForegroundFollowSuppressedCore(handle))
            {
                return true;
            }

            if (expectedProgrammaticHandle == default)
            {
                return false;
            }

            bool isInsideProgrammaticWindow = Stopwatch.GetElapsedTime(expectedProgrammaticAtTimestamp) < ProgrammaticForegroundWindow;

            if (!isInsideProgrammaticWindow)
            {
                ClearExpectedProgrammaticActivationCore();
                return false;
            }

            if (handle == expectedProgrammaticHandle)
            {
                ClearExpectedProgrammaticActivationCore();
                return true;
            }

            return false;
        }
    }

    public IntPtr GetTrackedForegroundWindow()
    {
        lock (syncRoot)
        {
            return trackedForegroundWindowHandle;
        }
    }

    public void SetTrackedForegroundWindow(nint windowHandle)
    {
        if (windowHandle == default || !store.TryGet(windowHandle, out _))
        {
            return;
        }

        lock (syncRoot)
        {
            CancelPendingForegroundFollowCore();
            trackedForegroundWindowHandle = windowHandle;
        }
    }

    private void RecordForegroundWindow(IntPtr handle, bool isTracked)
    {
        lock (syncRoot)
        {
            foregroundWindowHandle = handle;

            if (isTracked)
            {
                trackedForegroundWindowHandle = handle;
            }
        }
    }

    public void SuppressForegroundFollow()
    {
        lock (syncRoot)
        {
            StartForegroundFollowSuppressionCore(false);
        }
    }

    private void SuppressAllForegroundFollow()
    {
        lock (syncRoot)
        {
            StartForegroundFollowSuppressionCore(true);
        }
    }

    private void StartForegroundFollowSuppressionCore(bool suppressAll)
    {
        foregroundFollowSuppressedAtTimestamp = Stopwatch.GetTimestamp();
        suppressedForegroundWindowHandle = foregroundWindowHandle;
        suppressAllForegroundFollow = suppressAll;
        CancelPendingForegroundFollowCore();
    }

    private bool IsForegroundFollowSuppressedCore(IntPtr handle)
    {
        if (foregroundFollowSuppressedAtTimestamp == 0)
        {
            return false;
        }

        if (Stopwatch.GetElapsedTime(foregroundFollowSuppressedAtTimestamp) < ForegroundFollowSuppressionWindow)
        {
            return suppressAllForegroundFollow || handle == suppressedForegroundWindowHandle;
        }

        foregroundFollowSuppressedAtTimestamp = 0;
        suppressedForegroundWindowHandle = default;
        suppressAllForegroundFollow = false;
        return false;
    }

    private void CancelPendingForegroundFollowCore()
    {
        foregroundFollowGeneration++;

        if (foregroundFollowCancellationTokenSource is null)
        {
            return;
        }

        TryCancel(foregroundFollowCancellationTokenSource);
        foregroundFollowCancellationTokenSource = null;
    }

    private void CleanupForegroundFollowDelay(long generation, CancellationTokenSource cancellationTokenSource)
    {
        lock (syncRoot)
        {
            if (generation == foregroundFollowGeneration && ReferenceEquals(foregroundFollowCancellationTokenSource, cancellationTokenSource))
            {
                foregroundFollowCancellationTokenSource = null;
            }
        }

        cancellationTokenSource.Dispose();
    }

    private void ClearExpectedProgrammaticActivationCore()
    {
        expectedProgrammaticHandle = default;
        expectedProgrammaticAtTimestamp = 0;
    }

    private void SetNavigationTarget(int page, double offset)
    {
        lock (syncRoot)
        {
            navigationTargetPage = page;
            navigationTargetOffset = offset;
        }
    }

    private bool IsNavigationSettled(int page, double offset)
    {
        double visualOffset = scroller.VisualOffset;

        if (!geometry.IsFinite(visualOffset))
        {
            return false;
        }

        lock (syncRoot)
        {
            return navigationTargetPage == page &&
                geometry.AreClose(navigationTargetOffset, offset) &&
                geometry.AreClose(visualOffset, offset);
        }
    }

    private bool TryGetWorkspaceWidth(out int workspaceWidth)
    {
        workspaceWidth = workspace.Width;
        return workspaceWidth > 0;
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
}

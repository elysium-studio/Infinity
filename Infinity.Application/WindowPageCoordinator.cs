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
    IDispatcher dispatcher) :
    IWindowNavigationCoordinator,
    IForegroundWindowCoordinator
{
    private const double ScrollTolerance = 2.0;
    private const double MeaningfulVisibilityRatio = 0.60;

    private static readonly TimeSpan ProgrammaticForegroundWindow = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan ForegroundFollowDeferDelay = TimeSpan.FromMilliseconds(80);
    private static readonly TimeSpan ForegroundFollowSuppressionWindow = TimeSpan.FromMilliseconds(900);

    private readonly Lock syncRoot = new();

    private CancellationTokenSource? foregroundFollowCancellationTokenSource;
    private IntPtr expectedProgrammaticHandle;
    private IntPtr foregroundWindowHandle;
    private long expectedProgrammaticAtTimestamp;
    private long foregroundFollowSuppressedAtTimestamp;
    private long foregroundFollowGeneration;

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

        int windowPage = GetWindowCenterPage(trackedWindow, workspaceWidth);
        double targetOffset = windowPage * (double)workspaceWidth;

        if (AreClose(scroller.VisualOffset, targetOffset))
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

        if (IsWindowFullyVisible(trackedWindow, workspaceWidth))
        {
            return;
        }

        int windowPage = GetWindowPage(trackedWindow, workspaceWidth);
        double targetOffset = GetTargetOffset(trackedWindow, workspaceWidth, windowPage);

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

        RecordForegroundWindow(handle);

        if (!store.TryGet(handle, out TrackedWindow? trackedWindow))
        {
            return;
        }

        if (!TryGetWorkspaceWidth(out int workspaceWidth))
        {
            return;
        }

        if (IsWindowMeaningfullyVisible(trackedWindow, workspaceWidth))
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

        SuppressForegroundFollow();

        lock (syncRoot)
        {
            if (handle == foregroundWindowHandle)
            {
                foregroundWindowHandle = default;
            }
        }
    }

    public void HandleWindowMinimizeEnded(IntPtr handle)
    {
        if (handle == default)
        {
            return;
        }

        SuppressForegroundFollow();

        lock (syncRoot)
        {
            foregroundWindowHandle = handle;
        }

        NavigateToPage(handle);
    }

    public void NotifyWindowClosed(IntPtr handle)
    {
        lock (syncRoot)
        {
            StartForegroundFollowSuppressionCore();

            if (handle != default && handle == expectedProgrammaticHandle)
            {
                ClearExpectedProgrammaticActivationCore();
            }

            if (handle != default && handle == foregroundWindowHandle)
            {
                foregroundWindowHandle = default;
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

    public void CompleteNavigation()
    {
        IntPtr handle;

        lock (syncRoot)
        {
            if (navigationTargetPage < 0 || !AreClose(scroller.VisualOffset, navigationTargetOffset))
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

        if (!ShouldCommitForegroundFollow(generation))
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

        if (IsWindowMeaningfullyVisible(trackedWindow, workspaceWidth))
        {
            return;
        }

        int windowPage = GetWindowPage(trackedWindow, workspaceWidth);
        double targetOffset = GetTargetOffset(trackedWindow, workspaceWidth, windowPage);

        PendingActivation = handle;

        if (IsNavigationSettled(windowPage, targetOffset))
        {
            RequestActivation(handle);
            return;
        }

        SetNavigationTarget(windowPage, targetOffset);
        scroller.ScrollTo(targetOffset);
    }

    private bool ShouldCommitForegroundFollow(long generation)
    {
        lock (syncRoot)
        {
            if (generation != foregroundFollowGeneration)
            {
                return false;
            }

            if (IsForegroundFollowSuppressedCore())
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
            if (IsForegroundFollowSuppressedCore())
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

    private void RecordForegroundWindow(IntPtr handle)
    {
        lock (syncRoot)
        {
            foregroundWindowHandle = handle;
        }
    }

    public void SuppressForegroundFollow()
    {
        lock (syncRoot)
        {
            StartForegroundFollowSuppressionCore();
        }
    }

    private void StartForegroundFollowSuppressionCore()
    {
        foregroundFollowSuppressedAtTimestamp = Stopwatch.GetTimestamp();
        CancelPendingForegroundFollowCore();
    }

    private bool IsForegroundFollowSuppressedCore()
    {
        if (foregroundFollowSuppressedAtTimestamp == 0)
        {
            return false;
        }

        if (Stopwatch.GetElapsedTime(foregroundFollowSuppressedAtTimestamp) < ForegroundFollowSuppressionWindow)
        {
            return true;
        }

        foregroundFollowSuppressedAtTimestamp = 0;
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

        if (!IsFinite(visualOffset))
        {
            return false;
        }

        lock (syncRoot)
        {
            return navigationTargetPage == page &&
                AreClose(navigationTargetOffset, offset) &&
                AreClose(visualOffset, offset);
        }
    }

    private bool TryGetWorkspaceWidth(out int workspaceWidth)
    {
        workspaceWidth = workspace.Width;
        return workspaceWidth > 0;
    }

    private bool IsWindowFullyVisible(TrackedWindow trackedWindow, int workspaceWidth)
    {
        double viewportLeft = scroller.VisualOffset;

        if (!IsFinite(viewportLeft))
        {
            return false;
        }

        double viewportRight = viewportLeft + workspaceWidth;

        return IsFullyInView(trackedWindow, viewportLeft, viewportRight);
    }

    private bool IsWindowMeaningfullyVisible(TrackedWindow trackedWindow, int workspaceWidth)
    {
        double viewportLeft = scroller.VisualOffset;

        if (!IsFinite(viewportLeft))
        {
            return false;
        }

        double viewportRight = viewportLeft + workspaceWidth;

        return IsMeaningfullyInView(trackedWindow, viewportLeft, viewportRight);
    }

    private static int GetWindowPage(TrackedWindow trackedWindow, int workspaceWidth)
    {
        double canvasX = GetSafeCanvasX(trackedWindow);

        if (workspaceWidth <= 0)
        {
            return 0;
        }

        double page = Math.Floor(canvasX / workspaceWidth);

        if (page > int.MaxValue)
        {
            return int.MaxValue;
        }

        if (page < int.MinValue)
        {
            return int.MinValue;
        }

        return (int)page;
    }

    private static int GetWindowCenterPage(TrackedWindow trackedWindow, int workspaceWidth)
    {
        if (workspaceWidth <= 0)
        {
            return 0;
        }

        double windowCenter = GetSafeCanvasX(trackedWindow) + (GetSafeWidth(trackedWindow) / 2.0);
        double page = Math.Floor(windowCenter / workspaceWidth);

        if (page > int.MaxValue)
        {
            return int.MaxValue;
        }

        if (page < int.MinValue)
        {
            return int.MinValue;
        }

        return Math.Max(0, (int)page);
    }

    private static double GetTargetOffset(TrackedWindow trackedWindow, int workspaceWidth, int windowPage)
    {
        double windowLeft = GetSafeCanvasX(trackedWindow);
        double windowWidth = GetSafeWidth(trackedWindow);
        double windowCenter = windowLeft + windowWidth / 2.0;
        double targetOffset = windowCenter - workspaceWidth / 2.0;
        double pageLeft = windowPage * (double)workspaceWidth;

        if (!IsFinite(targetOffset))
        {
            return pageLeft;
        }

        return Math.Max(pageLeft, targetOffset);
    }

    private static bool IsFullyInView(TrackedWindow trackedWindow, double viewportLeft, double viewportRight)
    {
        if (!IsFinite(viewportLeft) || !IsFinite(viewportRight) || viewportRight < viewportLeft)
        {
            return false;
        }

        double windowLeft = GetSafeCanvasX(trackedWindow);
        double windowRight = windowLeft + GetSafeWidth(trackedWindow);

        return windowLeft >= viewportLeft - ScrollTolerance &&
            windowRight <= viewportRight + ScrollTolerance;
    }

    private static bool IsMeaningfullyInView(TrackedWindow trackedWindow, double viewportLeft, double viewportRight)
    {
        if (!IsFinite(viewportLeft) || !IsFinite(viewportRight) || viewportRight < viewportLeft)
        {
            return false;
        }

        double windowLeft = GetSafeCanvasX(trackedWindow);
        double windowWidth = GetSafeWidth(trackedWindow);
        double windowRight = windowLeft + windowWidth;

        if (windowWidth <= 0)
        {
            return false;
        }

        if (windowLeft >= viewportLeft - ScrollTolerance && windowRight <= viewportRight + ScrollTolerance)
        {
            return true;
        }

        double windowCenter = windowLeft + windowWidth / 2.0;

        if (windowCenter >= viewportLeft && windowCenter <= viewportRight)
        {
            return true;
        }

        double visibleLeft = Math.Max(windowLeft, viewportLeft);
        double visibleRight = Math.Min(windowRight, viewportRight);
        double visibleWidth = Math.Max(0, visibleRight - visibleLeft);
        double visibleRatio = visibleWidth / windowWidth;

        return visibleRatio >= MeaningfulVisibilityRatio;
    }

    private static double GetSafeCanvasX(TrackedWindow trackedWindow)
    {
        if (!IsFinite(trackedWindow.CanvasX))
        {
            return 0;
        }

        return trackedWindow.CanvasX;
    }

    private static double GetSafeWidth(TrackedWindow trackedWindow)
    {
        if (!IsFinite(trackedWindow.Width) || trackedWindow.Width < 0)
        {
            return 0;
        }

        return trackedWindow.Width;
    }

    private static bool AreClose(double left, double right)
    {
        if (!IsFinite(left) || !IsFinite(right))
        {
            return false;
        }

        return Math.Abs(left - right) < ScrollTolerance;
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
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

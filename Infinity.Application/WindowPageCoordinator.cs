using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Infinity.Application;

public class WindowPageCoordinator(IWindowStore store,
    IPager pager,
    IScroller scroller,
    IWorkspace workspace,
    IWindowActivator activator,
    IDispatcher dispatcher) :
    IWindowPageCoordinator
{
    private const double ScrollTolerance = 2.0;

    private static readonly TimeSpan ProgrammaticRefocusWindow = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan FocusFollowDeferDelay = TimeSpan.FromMilliseconds(80);

    private readonly object syncRoot = new();

    private CancellationTokenSource? focusFollowCancellationTokenSource;
    private bool suppressNextFocusFollow;
    private IntPtr expectedProgrammaticHandle;
    private long expectedProgrammaticAtTimestamp;
    private long focusFollowGeneration;

    private int navigationTargetPage = -1;
    private double navigationTargetOffset = -1;
    private int pageBeforeFilter = -1;
    private IntPtr pendingActivation;

    public event EventHandler<NavigationStartedEventArgs>? NavigationStarted;

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

    public int PageBeforeFilter
    {
        get
        {
            lock (syncRoot)
            {
                return pageBeforeFilter;
            }
        }
        set
        {
            lock (syncRoot)
            {
                pageBeforeFilter = value;
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

        int windowPage = GetWindowPage(trackedWindow, workspaceWidth);

        if (windowPage != pager.CurrentPage)
        {
            double targetOffset = windowPage * (double)workspaceWidth;

            SetNavigationTarget(windowPage, targetOffset);
            NavigationStarted?.Invoke(this, new NavigationStartedEventArgs(windowPage));
            pager.NavigateToPage(windowPage);
        }

        RequestActivation(handle);
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

        int windowPage = GetWindowPage(trackedWindow, workspaceWidth);
        double targetOffset = GetTargetOffset(trackedWindow, workspaceWidth, windowPage);

        if (IsNavigationSettled(windowPage, targetOffset))
        {
            return;
        }

        SetNavigationTarget(windowPage, targetOffset);
        scroller.ScrollTo(targetOffset);
    }

    public void HandleFocusChanged(IntPtr handle)
    {
        if (handle == default)
        {
            return;
        }

        if (ShouldIgnoreFocusChange(handle))
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

        double viewportLeft = scroller.VisualOffset;

        if (!IsFinite(viewportLeft))
        {
            return;
        }

        double viewportRight = viewportLeft + workspaceWidth;

        bool isCompletelyOutOfView = IsCompletelyOutOfView(trackedWindow, viewportLeft, viewportRight);
        bool isSpanningPages = IsSpanningPages(trackedWindow, workspaceWidth);

        if (!isCompletelyOutOfView && !isSpanningPages)
        {
            return;
        }

        QueueFocusFollow(handle);
    }

    public void NotifyWindowClosed(IntPtr handle)
    {
        lock (syncRoot)
        {
            suppressNextFocusFollow = true;
            CancelPendingFocusFollowCore();

            if (handle != default && handle == expectedProgrammaticHandle)
            {
                ClearExpectedProgrammaticActivationCore();
            }
        }
    }

    public void ExpectProgrammaticActivation(IntPtr handle)
    {
        lock (syncRoot)
        {
            CancelPendingFocusFollowCore();

            if (handle == default)
            {
                ClearExpectedProgrammaticActivationCore();
                return;
            }

            expectedProgrammaticHandle = handle;
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

    private void RequestActivation(IntPtr handle)
    {
        WindowActivationRequested?.Invoke(this, EventArgs.Empty);
        ExpectProgrammaticActivation(handle);
        activator.Activate(handle);
    }

    private void QueueFocusFollow(IntPtr handle)
    {
        CancellationTokenSource cancellationTokenSource = new();
        long generation;

        lock (syncRoot)
        {
            CancelPendingFocusFollowCore();

            focusFollowGeneration++;
            generation = focusFollowGeneration;
            focusFollowCancellationTokenSource = cancellationTokenSource;
        }

        _ = DelayAndCommitFocusFollowAsync(generation, handle, cancellationTokenSource);
    }

    private async Task DelayAndCommitFocusFollowAsync(long generation, IntPtr handle, CancellationTokenSource cancellationTokenSource)
    {
        try
        {
            await Task.Delay(FocusFollowDeferDelay, cancellationTokenSource.Token).ConfigureAwait(false);
            dispatcher.Dispatch(() => RunCommitFocusFollow(generation, handle));
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
        finally
        {
            CleanupFocusFollowDelay(generation, cancellationTokenSource);
        }
    }

    private void RunCommitFocusFollow(long generation, IntPtr handle)
    {
        try
        {
            CommitFocusFollow(generation, handle);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void CommitFocusFollow(long generation, IntPtr handle)
    {
        if (handle == default)
        {
            return;
        }

        if (!ShouldCommitFocusFollow(generation))
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

        int windowPage = GetWindowPage(trackedWindow, workspaceWidth);
        double targetOffset = GetTargetOffset(trackedWindow, workspaceWidth, windowPage);

        PendingActivation = handle;

        if (IsNavigationSettled(windowPage, targetOffset))
        {
            return;
        }

        SetNavigationTarget(windowPage, targetOffset);
        scroller.ScrollTo(targetOffset);
    }

    private bool ShouldCommitFocusFollow(long generation)
    {
        lock (syncRoot)
        {
            if (generation != focusFollowGeneration)
            {
                return false;
            }

            if (suppressNextFocusFollow)
            {
                suppressNextFocusFollow = false;
                focusFollowGeneration++;
                return false;
            }

            return true;
        }
    }

    private bool ShouldIgnoreFocusChange(IntPtr handle)
    {
        bool shouldIgnore = false;

        lock (syncRoot)
        {
            if (expectedProgrammaticHandle != default)
            {
                bool isInsideProgrammaticWindow = Stopwatch.GetElapsedTime(expectedProgrammaticAtTimestamp) < ProgrammaticRefocusWindow;

                if (!isInsideProgrammaticWindow)
                {
                    ClearExpectedProgrammaticActivationCore();
                }
                else if (handle == expectedProgrammaticHandle)
                {
                    ClearExpectedProgrammaticActivationCore();
                    shouldIgnore = true;
                }
            }

            if (suppressNextFocusFollow)
            {
                suppressNextFocusFollow = false;
                shouldIgnore = true;
            }
        }

        return shouldIgnore;
    }

    private void CancelPendingFocusFollowCore()
    {
        focusFollowGeneration++;

        if (focusFollowCancellationTokenSource is null)
        {
            return;
        }

        TryCancel(focusFollowCancellationTokenSource);
        focusFollowCancellationTokenSource = null;
    }

    private void CleanupFocusFollowDelay(long generation, CancellationTokenSource cancellationTokenSource)
    {
        lock (syncRoot)
        {
            if (generation == focusFollowGeneration && ReferenceEquals(focusFollowCancellationTokenSource, cancellationTokenSource))
            {
                focusFollowCancellationTokenSource = null;
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

    private static bool IsCompletelyOutOfView(TrackedWindow trackedWindow, double viewportLeft, double viewportRight)
    {
        double windowLeft = GetSafeCanvasX(trackedWindow);
        double windowRight = windowLeft + GetSafeWidth(trackedWindow);

        return windowRight <= viewportLeft || windowLeft >= viewportRight;
    }

    private static bool IsSpanningPages(TrackedWindow trackedWindow, int workspaceWidth)
    {
        double windowLeft = GetSafeCanvasX(trackedWindow);
        double windowWidth = Math.Max(1.0, GetSafeWidth(trackedWindow));
        double windowRight = windowLeft + windowWidth - 1.0;

        return GetWindowPage(windowLeft, workspaceWidth) != GetWindowPage(windowRight, workspaceWidth);
    }

    private static int GetWindowPage(double canvasX, int workspaceWidth)
    {
        if (!IsFinite(canvasX) || workspaceWidth <= 0)
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
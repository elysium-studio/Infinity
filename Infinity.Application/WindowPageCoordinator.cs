using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;

namespace Infinity.Application;

public class WindowPageCoordinator(IWindowStore store,
    IPager pager,
    IScroller scroller,
    IWorkspace workspace,
    IWindowActivator activator,
    IDispatcher dispatcher) :
    IWindowPageCoordinator
{
    private static readonly TimeSpan ProgrammaticRefocusWindow = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan FocusFollowDeferDelay = TimeSpan.FromMilliseconds(80);

    private bool suppressNextFocusFollow;
    private IntPtr expectedProgrammaticHandle;
    private DateTime expectedProgrammaticAtUtc;
    private long focusFollowGeneration;

    public event EventHandler<NavigationStartedEventArgs>? NavigationStarted;
    public event EventHandler? WindowActivationRequested;

    public int NavigationTargetPage { get; set; } = -1;
    public double NavigationTargetOffset { get; set; } = -1;
    public int PageBeforeFilter { get; set; } = -1;
    public IntPtr PendingActivation { get; set; }

    public void NavigateTo(IntPtr handle)
    {
        if (!store.TryGet(handle, out TrackedWindow? trackedWindow))
        {
            return;
        }

        int workspaceWidth = workspace.Width;
        int windowPage = (int)Math.Floor((double)trackedWindow.CanvasX / workspaceWidth);

        if (windowPage != pager.CurrentPage)
        {
            double targetOffset = windowPage * (double)workspaceWidth;
            NavigationTargetPage = windowPage;
            NavigationTargetOffset = targetOffset;
            NavigationStarted?.Invoke(this, new NavigationStartedEventArgs(windowPage));
            pager.NavigateToPage(windowPage);
        }

        WindowActivationRequested?.Invoke(this, EventArgs.Empty);
        ExpectProgrammaticActivation(handle);
        activator.Activate(handle);
    }

    public void NavigateToPage(IntPtr handle)
    {
        if (!store.TryGet(handle, out TrackedWindow? trackedWindow))
        {
            return;
        }

        int workspaceWidth = workspace.Width;
        int windowPage = (int)Math.Floor((double)trackedWindow.CanvasX / workspaceWidth);

        double windowCenter = trackedWindow.CanvasX + trackedWindow.Width / 2.0;
        double targetOffset = windowCenter - workspaceWidth / 2.0;

        targetOffset = Math.Max(windowPage * workspaceWidth, targetOffset);

        if (NavigationTargetPage == windowPage && Math.Abs(scroller.VisualOffset - targetOffset) < 2)
        {
            return;
        }

        NavigationTargetPage = windowPage;
        NavigationTargetOffset = targetOffset;
        scroller.ScrollTo(targetOffset);
    }

    public void HandleFocusChanged(IntPtr handle)
    {
        if (!store.TryGet(handle, out TrackedWindow? trackedWindow))
        {
            return;
        }

        bool isProgrammaticRefocus = handle == expectedProgrammaticHandle &&
            DateTime.UtcNow - expectedProgrammaticAtUtc < ProgrammaticRefocusWindow;

        bool isClosureInducedRefocus = suppressNextFocusFollow;

        if (isProgrammaticRefocus)
        {
            expectedProgrammaticHandle = default;
            return;
        }

        if (isClosureInducedRefocus)
        {
            suppressNextFocusFollow = false;
            return;
        }

        int workspaceWidth = workspace.Width;
        double viewportLeft = scroller.VisualOffset;
        double viewportRight = viewportLeft + workspaceWidth;
        double windowLeft = trackedWindow.CanvasX;
        double windowRight = trackedWindow.CanvasX + trackedWindow.Width;

        bool isCompletelyOutOfView = windowRight <= viewportLeft || windowLeft >= viewportRight;
        bool isSpanningPages = (int)Math.Floor(windowLeft / workspaceWidth) != (int)Math.Floor((windowRight - 1) / workspaceWidth);

        if (!isCompletelyOutOfView && !isSpanningPages)
        {
            return;
        }

        long generation = ++focusFollowGeneration;

        Task.Delay(FocusFollowDeferDelay).ContinueWith(_ =>
            dispatcher.Dispatch(() => CommitFocusFollow(generation, handle)));
    }

    public void NotifyWindowClosed(IntPtr handle)
    {
        suppressNextFocusFollow = true;
    }

    public void ExpectProgrammaticActivation(IntPtr handle)
    {
        expectedProgrammaticHandle = handle;
        expectedProgrammaticAtUtc = DateTime.UtcNow;
    }

    public void Activate(IntPtr handle)
    {
        ExpectProgrammaticActivation(handle);
        activator.Activate(handle);
    }

    private void CommitFocusFollow(long generation, IntPtr handle)
    {
        if (generation != focusFollowGeneration)
        {
            return;
        }

        if (suppressNextFocusFollow)
        {
            suppressNextFocusFollow = false;
            return;
        }

        if (!store.TryGet(handle, out TrackedWindow? trackedWindow))
        {
            return;
        }

        int workspaceWidth = workspace.Width;
        int windowPage = (int)Math.Floor((double)trackedWindow.CanvasX / workspaceWidth);

        PendingActivation = handle;

        if (NavigationTargetPage != windowPage)
        {
            double windowCenter = trackedWindow.CanvasX + trackedWindow.Width / 2.0;
            double targetOffset = windowCenter - workspaceWidth / 2.0;
            targetOffset = Math.Max(windowPage * workspaceWidth, targetOffset);

            NavigationTargetPage = windowPage;
            NavigationTargetOffset = targetOffset;
            scroller.ScrollTo(targetOffset);
        }
    }
}
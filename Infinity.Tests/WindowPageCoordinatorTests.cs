using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Infinity.Application;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;

namespace Infinity.Tests;

public class WindowPageCoordinatorTests
{
    [Fact]
    public void RestoredWindowNavigatesToItsStoredPage()
    {
        WindowStore store = new();
        TestScroller scroller = new();
        TestWindowActivator activator = new();
        WindowPageCoordinator coordinator = CreateCoordinator(store, scroller, activator);
        IntPtr handle = new(1);
        store.Add(CreateWindow(handle, 1000));

        coordinator.HandleWindowMinimizeStarted(handle);
        coordinator.HandleWindowMinimizeEnded(handle);

        Assert.Equal(1000, scroller.LastTargetOffset);
        Assert.Equal(1, coordinator.NavigationTargetPage);
        Assert.Equal(1000, coordinator.NavigationTargetOffset);
        Assert.Equal(0, activator.ActivationCount);
    }

    [Fact]
    public void RestoredWindowOnCurrentPageDoesNotScroll()
    {
        WindowStore store = new();
        TestScroller scroller = new();
        TestWindowActivator activator = new();
        WindowPageCoordinator coordinator = CreateCoordinator(store, scroller, activator);
        IntPtr handle = new(2);
        store.Add(CreateWindow(handle, 0));

        coordinator.HandleWindowMinimizeStarted(handle);
        coordinator.HandleWindowMinimizeEnded(handle);

        Assert.Null(scroller.LastTargetOffset);
        Assert.Equal(0, activator.ActivationCount);
    }

    [Fact]
    public void UntrackedRestoredWindowDoesNotScroll()
    {
        TestScroller scroller = new();
        TestWindowActivator activator = new();
        WindowPageCoordinator coordinator = CreateCoordinator(new WindowStore(), scroller, activator);

        coordinator.HandleWindowMinimizeStarted(new IntPtr(3));
        coordinator.HandleWindowMinimizeEnded(new IntPtr(3));

        Assert.Null(scroller.LastTargetOffset);
        Assert.Equal(0, activator.ActivationCount);
    }

    [Fact]
    public void CompletingNavigationActivatesThePendingWindowOnce()
    {
        WindowStore store = new();
        TestScroller scroller = new();
        TestWindowActivator activator = new();
        WindowPageCoordinator coordinator = CreateCoordinator(store, scroller, activator);
        IntPtr handle = new(4);
        store.Add(CreateWindow(handle, 1000));
        int completionCount = 0;
        coordinator.NavigationCompleted += (_, _) => completionCount++;

        coordinator.NavigateTo(handle);
        scroller.SetVisualOffset(1000);
        coordinator.CompleteNavigation();
        coordinator.CompleteNavigation();

        Assert.Equal(1, completionCount);
        Assert.Equal(1, activator.ActivationCount);
        Assert.Equal(-1, coordinator.NavigationTargetPage);
        Assert.Equal(-1, coordinator.NavigationTargetOffset);
        Assert.Equal(IntPtr.Zero, coordinator.PendingActivation);
    }

    [Fact]
    public void CancelingNavigationPreventsPendingWindowActivation()
    {
        WindowStore store = new();
        TestScroller scroller = new();
        TestWindowActivator activator = new();
        WindowPageCoordinator coordinator = CreateCoordinator(store, scroller, activator);
        IntPtr handle = new(13);
        store.Add(CreateWindow(handle, 1000));

        coordinator.NavigateTo(handle);
        coordinator.CancelNavigation();
        scroller.SetVisualOffset(1000);
        coordinator.CompleteNavigation();

        Assert.Equal(0, activator.ActivationCount);
        Assert.Equal(-1, coordinator.NavigationTargetPage);
        Assert.Equal(-1, coordinator.NavigationTargetOffset);
        Assert.Equal(IntPtr.Zero, coordinator.PendingActivation);
    }

    [Fact]
    public void OverviewSelectionAlignsTheContainingPageInsteadOfTheWindowCentre()
    {
        WindowStore store = new();
        TestScroller scroller = new();
        TestWindowActivator activator = new();
        WindowPageCoordinator coordinator = CreateCoordinator(store, scroller, activator);
        IntPtr handle = new(5);
        store.Add(new TrackedWindow
        {
            Handle = handle,
            CanvasX = 1750,
            CanvasY = 0,
            Width = 500,
            Height = 500
        });

        coordinator.NavigateTo(handle);

        Assert.Equal(2000, scroller.LastTargetOffset);
        Assert.Equal(2, coordinator.NavigationTargetPage);
        Assert.Equal(2000, coordinator.NavigationTargetOffset);
        Assert.Equal(0, activator.ActivationCount);
    }

    [Fact]
    public void OverviewSelectionAlignsThePageWhenTheWindowIsVisibleBetweenPages()
    {
        WindowStore store = new();
        TestScroller scroller = new();
        TestWindowActivator activator = new();
        WindowPageCoordinator coordinator = CreateCoordinator(store, scroller, activator);
        IntPtr handle = new(6);
        store.Add(new TrackedWindow
        {
            Handle = handle,
            CanvasX = 1750,
            CanvasY = 0,
            Width = 500,
            Height = 500
        });
        scroller.SetVisualOffset(1500);

        coordinator.NavigateTo(handle);

        Assert.Equal(2000, scroller.LastTargetOffset);
        Assert.Equal(0, activator.ActivationCount);
    }

    [Fact]
    public async Task TaskbarSelectionOfAnotherWindowIsNotLostDuringPageSuppression()
    {
        WindowStore store = new();
        TestScroller scroller = new();
        TestWindowActivator activator = new();
        WindowPageCoordinator coordinator = CreateCoordinator(store, scroller, activator);
        IntPtr firstVisualStudioWindow = new(7);
        IntPtr secondVisualStudioWindow = new(8);
        store.Add(CreateWindow(firstVisualStudioWindow, 0));
        store.Add(CreateWindow(secondVisualStudioWindow, 2000));

        coordinator.HandleForegroundWindowChanged(firstVisualStudioWindow);
        coordinator.SuppressForegroundFollow();
        coordinator.HandleForegroundWindowChanged(secondVisualStudioWindow);

        await WaitForAsync(() => scroller.LastTargetOffset.HasValue);

        Assert.Equal(2000, scroller.LastTargetOffset);
        Assert.Equal(secondVisualStudioWindow, coordinator.PendingActivation);
    }

    [Fact]
    public async Task PageSuppressionStillIgnoresTheExistingForegroundWindow()
    {
        WindowStore store = new();
        TestScroller scroller = new();
        TestWindowActivator activator = new();
        WindowPageCoordinator coordinator = CreateCoordinator(store, scroller, activator);
        IntPtr handle = new(9);
        TrackedWindow trackedWindow = CreateWindow(handle, 0);
        store.Add(trackedWindow);

        coordinator.HandleForegroundWindowChanged(handle);
        trackedWindow.CanvasX = 1000;
        coordinator.SuppressForegroundFollow();
        coordinator.HandleForegroundWindowChanged(handle);

        await Task.Delay(150);

        Assert.Null(scroller.LastTargetOffset);
    }

    [Fact]
    public void DirectActivationPromotesWindowWithoutRequestingNavigation()
    {
        TestScroller scroller = new();
        TestWindowActivator activator = new();
        WindowPageCoordinator coordinator = CreateCoordinator(new WindowStore(), scroller, activator);
        int activationRequestCount = 0;
        coordinator.WindowActivationRequested += (_, _) => activationRequestCount++;

        coordinator.Activate(new IntPtr(10));

        Assert.Equal(1, activator.ActivationCount);
        Assert.Equal(0, activationRequestCount);
        Assert.Null(scroller.LastTargetOffset);
    }

    [Fact]
    public void UntrackedOverlayDoesNotReplaceTheTrackedForegroundWindow()
    {
        WindowStore store = new();
        WindowPageCoordinator coordinator = CreateCoordinator(store,
            new TestScroller(),
            new TestWindowActivator());
        IntPtr applicationWindow = new(11);
        store.Add(CreateWindow(applicationWindow, 0));

        coordinator.HandleForegroundWindowChanged(applicationWindow);
        coordinator.HandleForegroundWindowChanged(new IntPtr(12));

        Assert.Equal(applicationWindow, coordinator.GetTrackedForegroundWindow());
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        DateTime timeout = DateTime.UtcNow.AddSeconds(2);

        while (!condition() && DateTime.UtcNow < timeout)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    private static WindowPageCoordinator CreateCoordinator(IWindowStore store,
        IScroller scroller,
        IWindowActivator activator) =>
        new(store,
            scroller,
            new TestWorkspace(),
            activator,
            new TestDispatcher());

    private static TrackedWindow CreateWindow(IntPtr handle, int canvasX) =>
        new()
        {
            Handle = handle,
            CanvasX = canvasX,
            CanvasY = 0,
            Width = 1000,
            Height = 1000
        };

    private class TestScroller :
        IScroller
    {
        public event EventHandler? ScrollStarted;

        public event EventHandler? ScrollStopped;

        public double? LastTargetOffset { get; private set; }

        public double VisualOffset { get; private set; }

        public void CancelNavigation()
        {
        }

        public void CommitPresentation()
        {
        }

        public void SetVisualOffset(double value) => VisualOffset = value;

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        public void OnTick()
        {
        }

        public void Reposition()
        {
        }

        public void Reset()
        {
        }

        public void ScrollBy(double delta)
        {
        }

        public void ScrollTo(double offset, bool animate = true)
        {
            LastTargetOffset = offset;
        }

        public void Start() => ScrollStarted?.Invoke(this, EventArgs.Empty);

        public void Stop() => ScrollStopped?.Invoke(this, EventArgs.Empty);
    }

    private class TestWorkspace :
        IWorkspace
    {
        public event EventHandler? WorkspaceLayoutChanged;

        public int Height => 1000;

        public int Width => 1000;

        public int WorkAreaX => 0;

        public int WorkAreaY => 0;

        public IntPtr GetCurrentWorkspace()
        {
            WorkspaceLayoutChanged?.Invoke(this, EventArgs.Empty);
            return IntPtr.Zero;
        }
    }

    private class TestWindowActivator :
        IWindowActivator
    {
        public int ActivationCount { get; private set; }

        public void Activate(IntPtr handle) => ActivationCount++;
    }

    private class TestDispatcher :
        IDispatcher
    {
        public void Dispatch(Action action) => action();
    }
}

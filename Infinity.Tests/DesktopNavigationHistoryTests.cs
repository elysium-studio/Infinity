using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Infinity.Application;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Infinity.Tests;

public class DesktopNavigationHistoryTests
{
    [Fact]
    public void BackAndForwardRestoreVisitedWindows()
    {
        using HistoryFixture fixture = new();
        TrackedWindow first = fixture.AddWindow(1, 100, "First");
        TrackedWindow second = fixture.AddWindow(2, 2100, "Second");
        fixture.Start();
        fixture.Focus(first);
        fixture.Focus(second);

        Assert.True(fixture.History.GoBack());
        Assert.Equal([first.Handle], fixture.Navigation.NavigatedHandles);
        Assert.True(fixture.History.CanGoForward);

        fixture.Focus(first);

        Assert.True(fixture.History.GoForward());
        Assert.Equal([first.Handle, second.Handle], fixture.Navigation.NavigatedHandles);
    }

    [Fact]
    public void NewDestinationAfterBackClearsForwardHistory()
    {
        using HistoryFixture fixture = new();
        TrackedWindow first = fixture.AddWindow(1, 100, "First");
        TrackedWindow second = fixture.AddWindow(2, 1100, "Second");
        TrackedWindow third = fixture.AddWindow(3, 2100, "Third");
        fixture.Start();
        fixture.Focus(first);
        fixture.Focus(second);
        fixture.History.GoBack();
        fixture.Focus(first);

        fixture.Focus(third);

        Assert.False(fixture.History.CanGoForward);
        Assert.Empty(fixture.History.ForwardEntries);
    }

    [Fact]
    public void OutOfOrderForegroundEventsDoNotCancelHistoryReplay()
    {
        using HistoryFixture fixture = new();
        TrackedWindow first = fixture.AddWindow(1, 100, "First");
        TrackedWindow second = fixture.AddWindow(2, 1100, "Second");
        fixture.Start();
        fixture.Focus(first);
        fixture.Focus(second);
        fixture.History.GoBack();

        fixture.Focus(second);
        fixture.Focus(first);

        Assert.True(fixture.History.CanGoForward);
        Assert.True(fixture.History.GoForward());
        Assert.Equal([first.Handle, second.Handle], fixture.Navigation.NavigatedHandles);
    }

    [Fact]
    public void RapidPageChangesCommitOnlyTheSettledPage()
    {
        using HistoryFixture fixture = new();
        fixture.Start();

        fixture.Pager.NavigateToPage(1);
        fixture.Pager.NavigateToPage(2);
        fixture.Scroller.RaiseStopped();

        Assert.True(fixture.History.CanGoBack);
        Assert.Single(fixture.History.BackEntries);
        Assert.Equal(0, fixture.History.BackEntries[0].Page);

        fixture.Pager.NavigatedPages.Clear();
        fixture.History.GoBack();
        Assert.Equal([0], fixture.Pager.NavigatedPages);
    }

    [Fact]
    public void ClosedAndReusedWindowHandlesAreNotReplayed()
    {
        using HistoryFixture fixture = new();
        TrackedWindow first = fixture.AddWindow(1, 100, "First");
        TrackedWindow second = fixture.AddWindow(2, 1100, "Second");
        fixture.Start();
        fixture.Focus(first);
        fixture.Focus(second);

        fixture.Store.Remove(first.Handle);
        fixture.Store.Add(HistoryFixture.CreateWindow(1, 2100, "Replacement"));

        Assert.DoesNotContain(fixture.History.BackEntries, entry => entry.WindowHandle == first.Handle);
        Assert.True(fixture.History.GoBack());
        Assert.Empty(fixture.Navigation.NavigatedHandles);
        Assert.Equal([0], fixture.Pager.NavigatedPages);
    }

    [Fact]
    public void ReplacingAWindowObjectWithTheSameHandlePrunesStaleEntriesImmediately()
    {
        using HistoryFixture fixture = new();
        TrackedWindow first = fixture.AddWindow(1, 100, "First");
        TrackedWindow second = fixture.AddWindow(2, 1100, "Second");
        fixture.Start();
        fixture.Focus(first);
        fixture.Focus(second);

        fixture.Store.Add(HistoryFixture.CreateWindow(1, 2100, "Replacement"));

        Assert.DoesNotContain(fixture.History.BackEntries, entry => entry.WindowHandle == first.Handle);
    }

    [Fact]
    public void MovedWindowsFollowTheirCurrentPage()
    {
        using HistoryFixture fixture = new();
        TrackedWindow first = fixture.AddWindow(1, 100, "First");
        TrackedWindow second = fixture.AddWindow(2, 1100, "Second");
        fixture.Start();
        fixture.Focus(first);
        fixture.Focus(second);

        first.CanvasX = 3100;
        fixture.Store.NotifyChanged(first.Handle);

        Assert.Equal(3, fixture.History.BackEntries.First(entry => entry.WindowHandle == first.Handle).Page);
        Assert.True(fixture.History.GoBack());
        Assert.Equal([first.Handle], fixture.Navigation.NavigatedHandles);
    }

    [Fact]
    public void StickyWindowVisitsRetainTheRecordedPage()
    {
        using HistoryFixture fixture = new();
        TrackedWindow sticky = fixture.AddWindow(1, 100, "Sticky");
        sticky.IsSticky = true;
        TrackedWindow other = fixture.AddWindow(2, 1100, "Other");
        fixture.Pager.SetCurrentPage(2);
        fixture.Start();
        fixture.Focus(sticky);
        fixture.Focus(other);

        sticky.CanvasX = 4100;
        fixture.Store.NotifyChanged(sticky.Handle);

        DesktopHistoryEntry entry = fixture.History.BackEntries.First(item => item.WindowHandle == sticky.Handle);
        Assert.Equal(2, entry.Page);

        fixture.History.GoBack();
        Assert.Equal([2], fixture.Pager.NavigatedPages);
        Assert.Equal([sticky.Handle], fixture.Navigation.ActivatedHandles);
    }

    [Fact]
    public void SelectingAnOlderEntryPreservesIntermediateForwardEntries()
    {
        using HistoryFixture fixture = new();
        TrackedWindow first = fixture.AddWindow(1, 100, "First");
        TrackedWindow second = fixture.AddWindow(2, 1100, "Second");
        TrackedWindow third = fixture.AddWindow(3, 2100, "Third");
        fixture.Start();
        fixture.Focus(first);
        fixture.Focus(second);
        fixture.Focus(third);
        DesktopHistoryEntry firstEntry = fixture.History.BackEntries.First(entry => entry.WindowHandle == first.Handle);

        Assert.True(fixture.History.NavigateTo(firstEntry.Id));

        Assert.Equal([first.Handle], fixture.Navigation.NavigatedHandles);
        Assert.Equal([second.Handle, third.Handle], fixture.History.ForwardEntries.Select(entry => entry.WindowHandle));
    }

    [Fact]
    public void CapacityAppliesAcrossBackAndForwardStacks()
    {
        using HistoryFixture fixture = new();
        fixture.Configuration.Update(true, 10, true, null, null);

        for (int index = 1; index <= 15; index++)
        {
            fixture.AddWindow(index, index * 1000, $"Window {index}");
        }

        fixture.Start();

        foreach (TrackedWindow window in fixture.Store.ToArray())
        {
            fixture.Focus(window);
        }

        Assert.Equal(10, fixture.History.BackEntries.Count);

        for (int index = 0; index < 4; index++)
        {
            fixture.History.GoBack();
            fixture.Focus(fixture.Store.First(window => window.Handle == fixture.Navigation.NavigatedHandles[^1]));
        }

        Assert.Equal(10, fixture.History.BackEntries.Count + fixture.History.ForwardEntries.Count);
    }

    [Fact]
    public void ReducingThePageLimitPrunesDestinationsOutsideTheNewRange()
    {
        using HistoryFixture fixture = new();
        TrackedWindow first = fixture.AddWindow(1, 100, "First");
        TrackedWindow distant = fixture.AddWindow(2, 4100, "Distant");
        TrackedWindow current = fixture.AddWindow(3, 1100, "Current");
        fixture.Start();
        fixture.Focus(first);
        fixture.Focus(distant);
        fixture.Focus(current);

        fixture.Pager.SetMaxPages(2);
        fixture.Configuration.Update(true, 100, true, null, null);

        Assert.DoesNotContain(fixture.History.BackEntries, entry => entry.Page >= 2);
        Assert.DoesNotContain(fixture.History.ForwardEntries, entry => entry.Page >= 2);
    }

    [Fact]
    public void DisablingHistoryClearsItAndStopsRecording()
    {
        using HistoryFixture fixture = new();
        TrackedWindow first = fixture.AddWindow(1, 100, "First");
        TrackedWindow second = fixture.AddWindow(2, 1100, "Second");
        fixture.Start();
        fixture.Focus(first);
        fixture.Focus(second);

        fixture.Configuration.Update(false, 100, true, null, null);
        fixture.Focus(first);

        Assert.False(fixture.History.IsEnabled);
        Assert.Empty(fixture.History.BackEntries);
        Assert.Empty(fixture.History.ForwardEntries);
        Assert.False(fixture.History.GoBack());
    }

    [Fact]
    public void StopUnsubscribesFromEveryInput()
    {
        using HistoryFixture fixture = new();
        TrackedWindow first = fixture.AddWindow(1, 100, "First");
        fixture.Start();
        fixture.History.Stop();

        fixture.Focus(first);
        fixture.Pager.NavigateToPage(2);
        fixture.Scroller.RaiseStopped();

        Assert.Empty(fixture.History.BackEntries);
        Assert.Empty(fixture.History.ForwardEntries);
    }

    private sealed class HistoryFixture : IDisposable
    {
        public WindowStore Store { get; } = new();
        public TestPager Pager { get; } = new();
        public TestScroller Scroller { get; } = new();
        public TestNavigationCoordinator Navigation { get; } = new();
        public TestForegroundWindowTracker ForegroundTracker { get; } = new();
        public TestForegroundWindowSource ForegroundSource { get; } = new();
        public DesktopHistoryConfiguration Configuration { get; } = new();
        public DesktopNavigationHistory History { get; }

        public HistoryFixture()
        {
            History = new DesktopNavigationHistory(Store,
                Pager,
                Scroller,
                Navigation,
                ForegroundTracker,
                ForegroundSource,
                new TestWorkspace(),
                new TestDispatcher(),
                Configuration,
                NullLogger<DesktopNavigationHistory>.Instance);
        }

        public TrackedWindow AddWindow(int handle, int canvasX, string title)
        {
            TrackedWindow window = CreateWindow(handle, canvasX, title);
            Store.Add(window);
            return window;
        }

        public static TrackedWindow CreateWindow(int handle, int canvasX, string title) => new()
        {
            Handle = new IntPtr(handle),
            CanvasX = canvasX,
            CanvasY = 100,
            Width = 800,
            Height = 600,
            Title = title
        };

        public void Start() => History.Start();

        public void Focus(TrackedWindow window)
        {
            ForegroundSource.Handle = window.Handle;
            ForegroundTracker.Raise(window.Handle);
        }

        public void Dispose()
        {
            History.Stop();
            Scroller.Dispose();
        }
    }

    private sealed class TestPager : IPager
    {
        public event Action<int>? PageChanged;

        public int CurrentPage { get; private set; }
        public int PageCount => MaxPages ?? 20;
        public int? MaxPages { get; private set; }
        public List<int> NavigatedPages { get; } = [];

        public void SetCurrentPage(int page) => CurrentPage = page;

        public void SetMaxPages(int? maxPages) => MaxPages = maxPages;

        public void NavigateToPage(int page)
        {
            CurrentPage = page;
            NavigatedPages.Add(page);
            PageChanged?.Invoke(page);
        }

        public void Start()
        {
        }

        public void Stop()
        {
        }
    }

    private sealed class TestScroller : IScroller
    {
        public event EventHandler? ScrollStarted;
        public event EventHandler? ScrollStopped;

        public double VisualOffset => 0;

        public void RaiseStopped() => ScrollStopped?.Invoke(this, EventArgs.Empty);

        public void Dispose() => GC.SuppressFinalize(this);

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
        }

        public void Start() => ScrollStarted?.Invoke(this, EventArgs.Empty);

        public void Stop()
        {
        }
    }

    private sealed class TestNavigationCoordinator : IWindowNavigationCoordinator
    {
        public event EventHandler<NavigationStartedEventArgs>? NavigationStarted
        {
            add { }
            remove { }
        }
        public event EventHandler? WindowActivationRequested;

        public int NavigationTargetPage { get; set; }
        public double NavigationTargetOffset { get; set; }
        public int PageBeforeFilter { get; set; } = -1;
        public IntPtr PendingActivation { get; set; }
        public List<IntPtr> NavigatedHandles { get; } = [];
        public List<IntPtr> ActivatedHandles { get; } = [];

        public void NavigateTo(IntPtr handle) => NavigatedHandles.Add(handle);

        public void NavigateToPage(IntPtr handle) => NavigatedHandles.Add(handle);

        public void Activate(IntPtr handle)
        {
            ActivatedHandles.Add(handle);
            WindowActivationRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class TestForegroundWindowTracker : IForegroundWindowTracker
    {
        public event EventHandler<IntPtr>? ForegroundWindowChanged;

        public void Raise(IntPtr handle) => ForegroundWindowChanged?.Invoke(this, handle);

        public void Start()
        {
        }

        public void Stop()
        {
        }

        public void NotifyForegroundWindowChanged(IntPtr windowHandle) => Raise(windowHandle);
    }

    private sealed class TestForegroundWindowSource : IForegroundWindowSource
    {
        public IntPtr Handle { get; set; }

        public IntPtr GetForegroundWindow() => Handle;
    }

    private sealed class TestWorkspace : IWorkspace
    {
        public event EventHandler? WorkspaceLayoutChanged;

        public int Width => 1000;
        public int Height => 800;
        public int WorkAreaX => 0;
        public int WorkAreaY => 0;

        public IntPtr GetCurrentWorkspace()
        {
            WorkspaceLayoutChanged?.Invoke(this, EventArgs.Empty);
            return IntPtr.Zero;
        }
    }

    private sealed class TestDispatcher : IDispatcher
    {
        public void Dispatch(Action action) => action();
    }
}

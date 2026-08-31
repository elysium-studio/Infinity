using Elysium.Platform.Abstractions;
using Infinity.Application;
using Infinity.Application.Abstractions;
using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopWindowDropNavigationCoordinatorTests
{
    [Fact]
    public void NavigatesToThePageContainingTheDroppedWindow()
    {
        WindowStore store = new();
        store.Add(new TrackedWindow
        {
            Handle = 1,
            CanvasX = 1200,
            CanvasY = 100,
            Width = 400,
            Height = 300
        });
        TestPager pager = new();
        DesktopWindowDropNavigationCoordinator coordinator = new(store, new TestWorkspace(), pager);

        bool navigated = coordinator.NavigateToDroppedWindow(1);

        Assert.True(navigated);
        Assert.Equal(1, pager.NavigatedPage);
    }

    [Fact]
    public void DoesNotNavigateWhenTheDroppedPageIsAlreadyCentered()
    {
        WindowStore store = new();
        store.Add(new TrackedWindow
        {
            Handle = 1,
            CanvasX = 1200,
            CanvasY = 100,
            Width = 400,
            Height = 300
        });
        TestPager pager = new() { CenteredPage = 1 };
        DesktopWindowDropNavigationCoordinator coordinator = new(store, new TestWorkspace(), pager);

        bool navigated = coordinator.NavigateToDroppedWindow(1);

        Assert.False(navigated);
        Assert.Null(pager.NavigatedPage);
    }

    [Fact]
    public void MissingWindowDoesNotNavigate()
    {
        TestPager pager = new();
        DesktopWindowDropNavigationCoordinator coordinator = new(new WindowStore(), new TestWorkspace(), pager);

        bool navigated = coordinator.NavigateToDroppedWindow(1);

        Assert.False(navigated);
        Assert.Null(pager.NavigatedPage);
    }

    private sealed class TestPager : IPager
    {
        public event Action<int>? PageChanged;

        public int CurrentPage => CenteredPage ?? 0;

        public int PageCount => 3;

        public int? MaxPages => null;

        public int? CenteredPage { get; init; }

        public int? NavigatedPage { get; private set; }

        public bool IsPageCentered(int page) => page == CenteredPage;

        public void NavigateToPage(int page)
        {
            NavigatedPage = page;
            PageChanged?.Invoke(page);
        }

        public void SetMaxPages(int? maxPages)
        {
        }

        public void Start()
        {
        }

        public void Stop()
        {
        }
    }

    private sealed class TestWorkspace : IWorkspace
    {
        public event EventHandler? WorkspaceLayoutChanged;

        public int Width => 1000;

        public int Height => 800;

        public int WorkAreaX => 0;

        public int WorkAreaY => 0;

        public nint GetCurrentWorkspace()
        {
            WorkspaceLayoutChanged?.Invoke(this, EventArgs.Empty);
            return 0;
        }
    }
}

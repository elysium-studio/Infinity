using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopDragBoundaryCalculatorTests
{
    [Fact]
    public void CenteredPageBoundsFollowTheScaledWorkspaceInsteadOfTheViewportEdges()
    {
        DesktopDragBoundaryCalculator calculator = CreateCalculator();
        calculator.SetWorkAreaOffsetY(50);

        DesktopDragBounds bounds = calculator.GetCenteredPageBounds(1600, 1000, 0.5);

        Assert.Equal(new DesktopDragBounds(550, 250, 1050, 650), bounds);
    }

    [Fact]
    public void CenteredPageConfinementKeepsThePointerInsideTheFocusedPage()
    {
        DesktopDragBoundaryCalculator calculator = CreateCalculator();
        calculator.SetWorkAreaOffsetY(50);

        (double x, double y) = calculator.ConstrainToCenteredPage(100, 900, 1600, 1000, 0.5);

        Assert.Equal(550, x);
        Assert.Equal(650, y);
    }

    private static DesktopDragBoundaryCalculator CreateCalculator() => new(
        new TestPager(),
        new TestScroller(),
        new TestWorkspace(),
        new DesktopPageLayoutCalculator());

    private sealed class TestPager : IPager
    {
        public event Action<int>? PageChanged;

        public int CurrentPage => 0;

        public int PageCount => 1;

        public int? MaxPages => null;

        public bool IsPageCentered(int page) => page == 0;

        public void NavigateToPage(int page) => PageChanged?.Invoke(page);

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

        public int Height => 800;

        public int Width => 1000;

        public int WorkAreaX => 0;

        public int WorkAreaY => 0;

        public nint GetCurrentWorkspace()
        {
            WorkspaceLayoutChanged?.Invoke(this, EventArgs.Empty);
            return 0;
        }
    }
}

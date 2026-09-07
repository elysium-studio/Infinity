using Elysium.Platform.Abstractions;
using Infinity.Application;
using Infinity.Application.Abstractions;
using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopWindowDragPositionResolverTests
{
    private readonly WindowStore store = new();
    private readonly TestWorkspace workspace = new();
    private readonly DesktopPageLayoutCalculator layoutCalculator = new();

    [Theory]
    [InlineData(0, -180, 100, 0, 100)]
    [InlineData(1, -180, 100, 1920, 100)]
    [InlineData(1, 1800, 100, 3440, 100)]
    [InlineData(2, 100, -20, 3940, 0)]
    [InlineData(2, 100, 1000, 3940, 540)]
    public void ImportedThumbnailDropsAreConstrainedToTheirDestinationPage(int page, double localX, double localY, double expectedX, double expectedY)
    {
        DesktopWindowDragPositionResolver resolver = CreateResolver(CreateWindow(100, 100, 400, 500));
        Assert.True(resolver.TryResolveOnPage(1, page, localX, localY, out DesktopWindowDragPosition position));
        Assert.Equal(expectedX, position.CanvasX);
        Assert.Equal(expectedY, position.CanvasY);
        double visualDelta = page * (workspace.Width + layoutCalculator.PageSpacing) + localX - 100;
        Assert.True(resolver.TryResolve(1, visualDelta, localY - 100, out DesktopWindowDragPosition normalDrop, page));
        Assert.Equal(normalDrop, position);
    }

    [Fact]
    public void ImportedDropAccountsForMonitorOriginAndTaskbar()
    {
        workspace.X = -1872;
        workspace.Y = 48;
        workspace.WidthValue = 1872;
        workspace.HeightValue = 1032;
        DesktopWindowDragPositionResolver resolver = CreateResolver(CreateWindow(-1500, 100, 800, 500));
        Assert.True(resolver.TryResolveOnPage(1, 0, -100, 1000, out DesktopWindowDragPosition position));
        Assert.Equal(-1872, position.CanvasX);
        Assert.Equal(580, position.CanvasY);
    }

    [Fact]
    public void ImportedOversizedWindowUsesThePageOriginWithoutResizing()
    {
        TrackedWindow window = CreateWindow(100, 100, 2400, 1200);
        DesktopWindowDragPositionResolver resolver = CreateResolver(window);
        Assert.True(resolver.TryResolveOnPage(1, 1, 500, 500, out DesktopWindowDragPosition position));
        Assert.Equal(new DesktopWindowDragPosition(1920, 0), position);
        Assert.Equal(2400, window.Width);
        Assert.Equal(1200, window.Height);
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(0, double.NaN, 0)]
    [InlineData(0, 0, double.PositiveInfinity)]
    public void InvalidImportedDropIsRejected(int page, double localX, double localY)
    {
        DesktopWindowDragPositionResolver resolver = CreateResolver(CreateWindow(100, 100, 400, 500));
        Assert.False(resolver.TryResolveOnPage(1, page, localX, localY, out _));
    }

    [Fact]
    public void PointerDestinationWinsWhenAWideThumbnailStillHasItsCenterOnTheSourcePage()
    {
        DesktopWindowDragPositionResolver resolver = CreateResolver(CreateWindow(100, 100, 1600, 500));
        Assert.True(resolver.TryResolve(new(1), 900, 0, out DesktopWindowDragPosition position, 1));
        Assert.Equal(1920, position.CanvasX);
    }

    [Theory]
    [InlineData(2120, 1984, 2, 4040)]
    [InlineData(4040, -1984, 1, 2120)]
    [InlineData(200, 5952, 3, 5960)]
    public void DroppingPreservesPageLocalPositionInEitherDirection(int sourceX, double visualDelta, int page, double expectedX)
    {
        DesktopWindowDragPositionResolver resolver = CreateResolver(CreateWindow(sourceX, 100, 400, 500));
        Assert.True(resolver.TryResolve(new(1), visualDelta, 0, out DesktopWindowDragPosition position, page));
        Assert.Equal(expectedX, position.CanvasX);
    }

    [Fact]
    public void DropPositionStaysWithinTheChosenPage()
    {
        DesktopWindowDragPositionResolver resolver = CreateResolver(CreateWindow(100, 100, 400, 500));
        Assert.True(resolver.TryResolve(new(1), 4000, 0, out DesktopWindowDragPosition position, 1));
        Assert.Equal(3440, position.CanvasX);
    }

    [Fact]
    public void InvalidDestinationIsRejected()
    {
        DesktopWindowDragPositionResolver resolver = CreateResolver(CreateWindow(100, 100, 400, 500));
        Assert.False(resolver.TryResolve(new(1), 100, 0, out _, -1));
    }

    [Fact]
    public void ResolveKeepsWindowAboveBottomTaskbar()
    {
        TrackedWindow window = CreateWindow(100, 100, 800, 500);
        DesktopWindowDragPositionResolver resolver = CreateResolver(window);
        bool resolved = resolver.TryResolve(window.Handle, 0, 900, out DesktopWindowDragPosition position);
        Assert.True(resolved);
        Assert.Equal(540, position.CanvasY);
    }


    [Fact]
    public void ResolveKeepsWindowInsideSideTaskbarBoundary()
    {
        workspace.X = 48;
        workspace.WidthValue = 1872;
        TrackedWindow window = CreateWindow(100, 100, 800, 500);
        DesktopWindowDragPositionResolver resolver = CreateResolver(window);
        bool resolved = resolver.TryResolve(window.Handle, -500, 0, out DesktopWindowDragPosition position);
        Assert.True(resolved);
        Assert.Equal(48, position.CanvasX);
    }


    private DesktopWindowDragPositionResolver CreateResolver(TrackedWindow window)
    {
        store.Add(window);
        return new(store, workspace, layoutCalculator);
    }


    private static TrackedWindow CreateWindow(int x, int y, int width, int height) => new()
    {
        Handle = new(1),
        CanvasX = x,
        CanvasY = y,
        Width = width,
        Height = height
    };

    private sealed class TestWorkspace : IWorkspace
    {
        public event EventHandler? WorkspaceLayoutChanged;

        public int X { get; set; }

        public int Y { get; set; }

        public int WidthValue { get; set; } = 1920;

        public int HeightValue { get; set; } = 1040;

        public int Width => WidthValue;

        public int Height => HeightValue;

        public int WorkAreaX => X;

        public int WorkAreaY => Y;

        public nint GetCurrentWorkspace()
        {
            WorkspaceLayoutChanged?.Invoke(this, EventArgs.Empty);
            return 0;
        }
    }
}

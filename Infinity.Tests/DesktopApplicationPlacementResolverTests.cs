using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopApplicationPlacementResolverTests
{
    private readonly TestWorkspace workspace = new();

    [Fact]
    public void PageTargetKeepsNaturalSizeAndCentresWindow()
    {
        DesktopApplicationPlacementResolver resolver = CreateResolver();
        TrackedWindow window = CreateWindow(800, 600);
        bool resolved = resolver.TryResolve(window, new DesktopApplicationTarget(2), 100, 40, out DesktopApplicationPlacement placement);
        Assert.True(resolved);
        Assert.False(placement.Resize);
        Assert.Equal(4500, placement.CanvasX);
        Assert.Equal(260, placement.CanvasY);
        Assert.Equal(800, placement.Width);
        Assert.Equal(600, placement.Height);
    }


    [Fact]
    public void SlotTargetUsesSnapLayoutAndRequestsResize()
    {
        DesktopApplicationPlacementResolver resolver = CreateResolver();
        TrackedWindow window = CreateWindow(800, 600);
        bool resolved = resolver.TryResolve(window, new DesktopApplicationTarget(1, DesktopSnapLayoutKind.Halves, 1), 100, 40, out DesktopApplicationPlacement placement);
        Assert.True(resolved);
        Assert.True(placement.Resize);
        Assert.Equal(2980, placement.CanvasX);
        Assert.Equal(40, placement.CanvasY);
        Assert.Equal(960, placement.Width);
        Assert.Equal(1040, placement.Height);
    }


    [Fact]
    public void OversizedWindowIsAlignedToPageWorkArea()
    {
        DesktopApplicationPlacementResolver resolver = CreateResolver();
        TrackedWindow window = CreateWindow(2400, 1200);
        bool resolved = resolver.TryResolve(window, new DesktopApplicationTarget(1), 100, 40, out DesktopApplicationPlacement placement);
        Assert.True(resolved);
        Assert.Equal(2020, placement.CanvasX);
        Assert.Equal(40, placement.CanvasY);
    }


    private DesktopApplicationPlacementResolver CreateResolver()
    {
        DesktopSnapPlacementResolver snapResolver = new(workspace, new DesktopSnapLayoutCatalog());
        return new(workspace, snapResolver, new(new TestWindowFrameGeometryReader()));
    }


    private static TrackedWindow CreateWindow(int width, int height) => new()
    {
        Handle = new(1),
        CanvasX = 0,
        CanvasY = 0,
        Width = width,
        Height = height
    };

    private sealed class TestWorkspace : IWorkspace
    {
        public event EventHandler? WorkspaceLayoutChanged;

        public int Height => 1040;

        public int Width => 1920;

        public int WorkAreaX => 100;

        public int WorkAreaY => 40;

        public nint GetCurrentWorkspace()
        {
            WorkspaceLayoutChanged?.Invoke(this, EventArgs.Empty);
            return 0;
        }
    }
}

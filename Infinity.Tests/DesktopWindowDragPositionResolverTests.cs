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
        return new DesktopWindowDragPositionResolver(store, workspace, layoutCalculator);
    }

    private static TrackedWindow CreateWindow(int x, int y, int width, int height) => new()
    {
        Handle = new nint(1),
        CanvasX = x,
        CanvasY = y,
        Width = width,
        Height = height
    };

    private sealed class TestWorkspace :
        IWorkspace
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

using Elysium.Platform.Abstractions;
using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopSnapPlacementResolverTests
{
    private readonly DesktopSnapPlacementResolver resolver = new(new TestWorkspace(), new DesktopSnapLayoutCatalog());

    [Fact]
    public void ResolveMapsPageAndSlotToCanvasCoordinates()
    {
        bool resolved = resolver.TryResolve(2, DesktopSnapLayoutKind.Halves, 1, 100, 20, out DesktopSnapPlacement placement);

        Assert.True(resolved);
        Assert.Equal(4906, placement.CanvasX);
        Assert.Equal(26, placement.CanvasY);
        Assert.Equal(948, placement.Width);
        Assert.Equal(1068, placement.Height);
    }

    [Theory]
    [InlineData(-1, DesktopSnapLayoutKind.Halves, 0)]
    [InlineData(0, DesktopSnapLayoutKind.None, 0)]
    [InlineData(0, DesktopSnapLayoutKind.Halves, 2)]
    public void ResolveRejectsInvalidTargets(int page, DesktopSnapLayoutKind layout, int slot)
    {
        Assert.False(resolver.TryResolve(page, layout, slot, 0, 0, out _));
    }

    [Fact]
    public void ResolveUsesWorkAreaHeightAboveTaskbar()
    {
        DesktopSnapPlacementResolver workAreaResolver = new(new TestWorkspace { HeightValue = 1040 }, new DesktopSnapLayoutCatalog());

        bool resolved = workAreaResolver.TryResolve(0, DesktopSnapLayoutKind.Halves, 0, 0, 0, out DesktopSnapPlacement placement);

        Assert.True(resolved);
        Assert.Equal(1028, placement.Height);
    }

    private sealed class TestWorkspace :
        IWorkspace
    {
        public event EventHandler? WorkspaceLayoutChanged;

        public int Height => HeightValue;

        public int HeightValue { get; init; } = 1080;

        public int Width => 1920;

        public int WorkAreaX => 0;

        public int WorkAreaY => 0;

        public IntPtr GetCurrentWorkspace()
        {
            WorkspaceLayoutChanged?.Invoke(this, EventArgs.Empty);
            return IntPtr.Zero;
        }
    }
}

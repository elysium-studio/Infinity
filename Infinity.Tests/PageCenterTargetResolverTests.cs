using Elysium.Platform.Abstractions;
using Infinity.Application;

namespace Infinity.Tests;

public sealed class PageCenterTargetResolverTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(490, 0)]
    [InlineData(510, 1000)]
    [InlineData(1490, 1000)]
    [InlineData(1510, 2000)]
    public void ResolvesThePageNearestTheViewportCentre(double offset, double expectedTarget)
    {
        PageCenterTargetResolver resolver = new(new TestWorkspace(width: 1000));

        bool resolved = resolver.TryResolve(offset, 0, 3000, out double target);

        Assert.Equal(expectedTarget != offset, resolved);
        Assert.Equal(expectedTarget, target);
    }

    [Fact]
    public void TargetHonoursTheConfiguredPageLimit()
    {
        PageCenterTargetResolver resolver = new(new TestWorkspace(width: 1000));

        bool resolved = resolver.TryResolve(2800, 0, 2000, out double target);

        Assert.True(resolved);
        Assert.Equal(2000, target);
    }

    [Fact]
    public void InvalidWorkspaceDoesNotRequestCentering()
    {
        PageCenterTargetResolver resolver = new(new TestWorkspace(width: 0));

        bool resolved = resolver.TryResolve(400, 0, 2000, out double target);

        Assert.False(resolved);
        Assert.Equal(400, target);
    }

    [Theory]
    [InlineData(0, 1, 1000)]
    [InlineData(1000, 1, 2000)]
    [InlineData(1000, -1, 0)]
    [InlineData(1000, 2, 3000)]
    public void ResolvesAdjacentPageFromTheCurrentDestination(double offset, int pageDelta, double expectedTarget)
    {
        PageCenterTargetResolver resolver = new(new TestWorkspace(width: 1000));

        bool resolved = resolver.TryResolveAdjacent(offset, pageDelta, 0, 3000, out double target);

        Assert.True(resolved);
        Assert.Equal(expectedTarget, target);
    }

    private sealed class TestWorkspace(int width) :
        IWorkspace
    {
        public event EventHandler? WorkspaceLayoutChanged;

        public int Height => 1000;

        public int Width => width;

        public int WorkAreaX => 0;

        public int WorkAreaY => 0;

        public IntPtr GetCurrentWorkspace()
        {
            WorkspaceLayoutChanged?.Invoke(this, EventArgs.Empty);
            return IntPtr.Zero;
        }
    }
}

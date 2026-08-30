using Elysium.Platform.Abstractions;
using Infinity.Application;

namespace Infinity.Tests;

public sealed class PageScrollSnapTargetResolverTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(490, 0)]
    [InlineData(510, 1000)]
    [InlineData(1490, 1000)]
    [InlineData(1510, 2000)]
    public void ResolvesThePageNearestTheViewportCentre(double offset, double expectedTarget)
    {
        PageScrollSnapTargetResolver resolver = new(new TestWorkspace(width: 1000));

        bool resolved = resolver.TryResolve(offset, 0, 3000, out double target);

        Assert.Equal(expectedTarget != offset, resolved);
        Assert.Equal(expectedTarget, target);
    }

    [Fact]
    public void TargetHonoursTheConfiguredPageLimit()
    {
        PageScrollSnapTargetResolver resolver = new(new TestWorkspace(width: 1000));

        bool resolved = resolver.TryResolve(2800, 0, 2000, out double target);

        Assert.True(resolved);
        Assert.Equal(2000, target);
    }

    [Fact]
    public void InvalidWorkspaceDoesNotRequestSnapping()
    {
        PageScrollSnapTargetResolver resolver = new(new TestWorkspace(width: 0));

        bool resolved = resolver.TryResolve(400, 0, 2000, out double target);

        Assert.False(resolved);
        Assert.Equal(400, target);
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

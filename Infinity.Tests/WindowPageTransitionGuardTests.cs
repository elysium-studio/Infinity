using Infinity.Application;

namespace Infinity.Tests;

public sealed class WindowPageTransitionGuardTests
{
    [Fact]
    public void MapsMaximizedGeometryBackToPreservedPage()
    {
        WindowPageTransitionGuard guard = new();
        guard.PreservePage(42, page: 2, workspaceWidth: 1920, workAreaX: 0);
        bool mapped = guard.TryMapToPreservedPage(42, candidateCanvasX: 1912, windowWidth: 1936, out int mappedCanvasX);
        Assert.True(mapped);
        Assert.Equal(3832, mappedCanvasX);
    }


    [Fact]
    public void LeavesUnmarkedWindowOnCandidatePage()
    {
        WindowPageTransitionGuard guard = new();
        bool mapped = guard.TryMapToPreservedPage(42, candidateCanvasX: 120, windowWidth: 800, out int mappedCanvasX);
        Assert.False(mapped);
        Assert.Equal(120, mappedCanvasX);
    }
}

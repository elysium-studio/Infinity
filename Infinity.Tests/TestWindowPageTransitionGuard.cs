using Infinity.Application.Abstractions;

namespace Infinity.Tests;

internal sealed class TestWindowPageTransitionGuard : IWindowPageTransitionGuard
{
    public void PreservePage(nint windowHandle, int page, int workspaceWidth, int workAreaX)
    {
    }


    public bool TryMapToPreservedPage(nint windowHandle, int candidateCanvasX, int windowWidth, out int mappedCanvasX)
    {
        mappedCanvasX = candidateCanvasX;
        return false;
    }


    public void Clear(nint windowHandle)
    {
    }
}

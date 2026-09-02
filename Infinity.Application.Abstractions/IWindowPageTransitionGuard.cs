namespace Infinity.Application.Abstractions;

public interface IWindowPageTransitionGuard
{
    void PreservePage(nint windowHandle, int page, int workspaceWidth, int workAreaX);

    bool TryMapToPreservedPage(nint windowHandle, int candidateCanvasX, int windowWidth, out int mappedCanvasX);

    void Clear(nint windowHandle);
}

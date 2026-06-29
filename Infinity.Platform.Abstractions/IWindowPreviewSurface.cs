namespace Infinity.Platform.Abstractions;

public interface IWindowPreviewSurface
{
    bool IsAvailable { get; }

    int LastHResult { get; }

    int LastBridgeHResult { get; }

    void Initialize(nint ownerWindowHandle);

    IWindowPreview? CreatePreview(nint windowHandle);

    void Render();

    void Commit();

    void Clear();
}
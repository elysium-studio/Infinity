namespace Infinity.Platform.Abstractions;

public interface IWindowPreviewSurface
{
    bool IsAvailable { get; }

    void Initialize(nint ownerWindowHandle);

    void SetTarget(nint sharedTargetHandle);

    IWindowPreview? CreatePreview(nint windowHandle);

    void Clear();
}

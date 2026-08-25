namespace Infinity.Platform.Abstractions;

public interface IWindowPreviewSurface
{
    bool IsAvailable { get; }

    void Initialize(nint ownerWindowHandle);

    IWindowPreview? CreatePreview(nint windowHandle);

    bool IsElevated(nint windowHandle);

    bool ShowElevated(nint windowHandle, int x, int y, int width, int height);

    void HideElevated(nint windowHandle);

    void Clear();
}

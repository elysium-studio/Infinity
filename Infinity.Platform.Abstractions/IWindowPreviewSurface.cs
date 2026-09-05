namespace Infinity.Platform.Abstractions;

public interface IWindowPreviewSurface
{
    bool IsAvailable { get; }


    void Initialize(nint ownerWindowHandle);

    void Clear();
}

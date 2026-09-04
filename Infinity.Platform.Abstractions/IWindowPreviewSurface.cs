namespace Infinity.Platform.Abstractions;

public interface IWindowPreviewSurface
{
    // Queried from input-synchronous native hooks. Implementations must return
    // cached state here, without invoking COM/WinRT or initialising resources.
    bool IsAvailable { get; }

    void Initialize(nint ownerWindowHandle);

    void Clear();
}

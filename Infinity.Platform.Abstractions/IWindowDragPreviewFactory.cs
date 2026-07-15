namespace Infinity.Platform.Abstractions;

public interface IWindowDragPreviewFactory
{
    IWindowDragPreview? Create(nint ownerWindowHandle,
        nint sourceWindowHandle,
        WindowPreviewBounds bounds);
}

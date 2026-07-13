namespace Infinity.Platform.Abstractions;

public interface IWindowPreviewOverlay
{
    void SetOverlayTarget(nint sharedTargetHandle, double width, double height, bool isVisible);

    void ClearOverlayTarget();
}

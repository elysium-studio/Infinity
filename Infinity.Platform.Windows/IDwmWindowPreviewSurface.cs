namespace Infinity.Platform.Windows;

public interface IDwmWindowPreviewSurface
{
    void Apply(DwmWindowPreview preview,
        nint sharedTargetHandle,
        double width,
        double height,
        bool isVisible);

    void Remove(DwmWindowPreview preview);
}
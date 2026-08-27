namespace Infinity.Platform.Windows;

public interface IDwmWindowPreviewSurface
{
    void Apply(DwmWindowPreview preview,
        nint sharedTargetHandle,
        double width,
        double height,
        bool isVisible);

    void RefreshSource(DwmWindowPreview preview);

    void Remove(DwmWindowPreview preview);
}

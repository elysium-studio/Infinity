namespace Infinity.Platform.Windows;

public interface IDwmWindowPreviewSurface
{
    void Apply(DwmWindowPreview preview,
        double x,
        double y,
        double width,
        double height,
        int zIndex,
        bool isVisible,
        bool isElevated);

    void Remove(DwmWindowPreview preview);
}

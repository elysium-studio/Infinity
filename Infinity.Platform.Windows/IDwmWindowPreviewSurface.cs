namespace Infinity.Platform.Windows;

public interface IDwmWindowPreviewSurface
{
    void Apply(DwmWindowPreview preview);

    void Remove(DwmWindowPreview preview);
}

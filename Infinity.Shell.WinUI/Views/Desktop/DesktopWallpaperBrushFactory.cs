using Microsoft.UI.Composition;

namespace Infinity.Shell.WinUI;

public sealed class DesktopWallpaperBrushFactory
{
    public CompositionSurfaceBrush Create(Compositor compositor, ICompositionSurface surface)
    {
        CompositionSurfaceBrush brush = compositor.CreateSurfaceBrush(surface);
        brush.Stretch = CompositionStretch.UniformToFill;
        brush.HorizontalAlignmentRatio = 0.5f;
        brush.VerticalAlignmentRatio = 0.5f;
        return brush;
    }
}

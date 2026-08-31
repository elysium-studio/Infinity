using Microsoft.UI.Composition;

namespace Infinity.Shell.WinUI;

public sealed class DesktopWallpaperBrushFactory
{
    internal const float WindowsFillVerticalAlignment = 1f / 3f;

    public CompositionSurfaceBrush Create(Compositor compositor, ICompositionSurface surface)
    {
        CompositionSurfaceBrush brush = compositor.CreateSurfaceBrush(surface);
        brush.Stretch = CompositionStretch.UniformToFill;
        brush.HorizontalAlignmentRatio = 0.5f;
        brush.VerticalAlignmentRatio = WindowsFillVerticalAlignment;
        return brush;
    }
}

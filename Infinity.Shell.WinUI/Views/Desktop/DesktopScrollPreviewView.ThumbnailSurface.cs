using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Hosting;

namespace Infinity.Shell.WinUI;

public sealed partial class DesktopScrollPreviewView
{
    private ExpressionAnimation? thumbnailScale;
    private ExpressionAnimation? thumbnailCenterPoint;

    private void StartThumbnailSurface()
    {
        StopThumbnailSurface();
        Visual source = ElementCompositionPreview.GetElementVisual(PreviewSurface);
        Visual target = ElementCompositionPreview.GetElementVisual(ThumbnailSurface);
        thumbnailScale = source.Compositor.CreateExpressionAnimation("source.Scale");
        thumbnailScale.SetReferenceParameter("source", source);
        thumbnailCenterPoint = source.Compositor.CreateExpressionAnimation("source.CenterPoint");
        thumbnailCenterPoint.SetReferenceParameter("source", source);
        target.StartAnimation(nameof(Visual.Scale), thumbnailScale);
        target.StartAnimation(nameof(Visual.CenterPoint), thumbnailCenterPoint);
    }

    private void StopThumbnailSurface()
    {
        Visual target = ElementCompositionPreview.GetElementVisual(ThumbnailSurface);
        target.StopAnimation(nameof(Visual.Scale));
        target.StopAnimation(nameof(Visual.CenterPoint));
        thumbnailScale?.Dispose();
        thumbnailScale = null;
        thumbnailCenterPoint?.Dispose();
        thumbnailCenterPoint = null;
    }
}

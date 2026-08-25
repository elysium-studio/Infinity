using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Infinity.Shell.WinUI;

public sealed class DesktopWindowPreviewFactory(IWindowPreviewSurface previewSurface,
    ITrackedWindowDragController dragController,
    ILogger<DesktopWindowPreviewFactory> logger)
{
    internal DesktopWindowPreview Create(Canvas canvas, nint windowHandle, double layoutScale)
    {
        Border previewHost = new()
        {
            Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
            CornerRadius = FluentVisualResources.GetOverlayCornerRadius(),
            IsHitTestVisible = false
        };
        Border host = new()
        {
            Background = FluentVisualResources.GetBrush("CardBackgroundFillColorDefaultBrush",
                Color.FromArgb(255, 32, 32, 32)),
            Child = previewHost,
            CornerRadius = FluentVisualResources.GetOverlayCornerRadius(),
            IsHitTestVisible = false,
            Shadow = new ThemeShadow()
        };

        canvas.Children.Add(host);
        ThumbnailCompositionPreview? preview = ThumbnailCompositionPreview.Create(previewSurface,
            windowHandle,
            previewHost,
            logger);
        return new DesktopWindowPreview(windowHandle,
            host,
            preview,
            dragController,
            layoutScale);
    }
}

using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Infinity.Shell.WinUI;

public sealed class DesktopWindowPreviewFactory(IWindowPreviewSurface previewSurface,
    ITrackedWindowDragController dragController,
    IWindowNavigationCoordinator windowNavigationCoordinator,
    DesktopWindowDragDeltaResolver dragDeltaResolver,
    ILogger<DesktopWindowPreviewFactory> logger)
{
    internal DesktopWindowPreview Create(Canvas canvas,
        Canvas focusCanvas,
        nint windowHandle,
        double layoutScale)
    {
        double visualScale = double.IsFinite(layoutScale) && layoutScale > 0 ? layoutScale : 1;
        double focusOuterMargin = 4 / visualScale;
        double focusPrimaryThickness = 2 / visualScale;
        double focusSecondaryMargin = 2 / visualScale;
        double focusSecondaryThickness = 1 / visualScale;
        CornerRadius cornerRadius = FluentVisualResources.GetOverlayCornerRadius();
        Grid focusVisual = new()
        {
            IsHitTestVisible = false,
            Margin = new Thickness(-focusOuterMargin),
            Visibility = Visibility.Collapsed
        };
        focusVisual.Children.Add(new Border
        {
            BorderBrush = FluentVisualResources.GetBrush("SystemControlFocusVisualPrimaryBrush",
                Color.FromArgb(255, 255, 255, 255)),
            BorderThickness = new Thickness(focusPrimaryThickness),
            CornerRadius = new CornerRadius(cornerRadius.TopLeft + focusOuterMargin)
        });
        focusVisual.Children.Add(new Border
        {
            BorderBrush = FluentVisualResources.GetBrush("SystemControlFocusVisualSecondaryBrush",
                Color.FromArgb(255, 0, 0, 0)),
            BorderThickness = new Thickness(focusSecondaryThickness),
            CornerRadius = new CornerRadius(cornerRadius.TopLeft + focusSecondaryMargin),
            Margin = new Thickness(focusSecondaryMargin)
        });
        Border previewHost = new()
        {
            Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
            CornerRadius = cornerRadius,
            IsHitTestVisible = false
        };
        Border host = new()
        {
            Background = FluentVisualResources.GetBrush("CardBackgroundFillColorDefaultBrush",
                Color.FromArgb(255, 32, 32, 32)),
            Child = previewHost,
            CornerRadius = cornerRadius,
            IsHitTestVisible = false,
            Shadow = new ThemeShadow()
        };
        Border focusHost = new()
        {
            Child = focusVisual,
            IsHitTestVisible = false
        };

        canvas.Children.Add(host);
        focusCanvas.Children.Add(focusHost);
        ThumbnailCompositionPreview? preview = ThumbnailCompositionPreview.Create(previewSurface,
            windowHandle,
            previewHost,
            logger);
        return new DesktopWindowPreview(windowHandle,
            host,
            focusHost,
            preview,
            focusVisual,
            dragController,
            windowNavigationCoordinator,
            dragDeltaResolver,
            layoutScale);
    }
}

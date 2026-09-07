using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Infinity.Shell.WinUI;

public sealed class DesktopWindowPreviewFactory(WindowCapturePreviewSurface previewSurface, ITrackedWindowDragController dragController, DesktopOverviewDragScroller overviewDragScroller, DesktopWindowDragPositionResolver dragPositionResolver, DesktopDragBoundaryCalculator dragBoundaryCalculator, DesktopDragCursorConfinement cursorConfinement, DesktopWindowPlacementCoordinator windowPlacementCoordinator, DesktopWindowContextMenuBuilder contextMenuBuilder, DesktopWindowDragFrames dragFrames, IPager pager, DesktopWindowThrowPlacementResolver throwPlacementResolver, PageLayoutStore pageLayouts, IWindowIconSource icons, ILogger<DesktopWindowPreviewFactory> logger)
{
    internal DesktopWindowPreview Create(Canvas backgroundCanvas, Canvas canvas, Canvas focusCanvas, nint windowHandle, double layoutScale)
    {
        double visualScale = double.IsFinite(layoutScale) && layoutScale > 0 ? layoutScale : 1;
        double focusOuterMargin = 4 / visualScale;
        double focusPrimaryThickness = 2 / visualScale;
        double focusSecondaryMargin = 2 / visualScale;
        double focusSecondaryThickness = 1 / visualScale;
        CornerRadius cornerRadius = FluentVisualResources.GetOverlayCornerRadius();
        Grid selectionVisual = new()
        {
            IsHitTestVisible = false,
            Margin = new(-focusPrimaryThickness),
            Visibility = Visibility.Collapsed
        };
        selectionVisual.Children.Add(new Border { Background = FluentVisualResources.GetBrush("AccentFillColorDefaultBrush", Color.FromArgb(255, 0, 120, 212)), CornerRadius = new(cornerRadius.TopLeft + focusPrimaryThickness), Opacity = 0.12 });
        selectionVisual.Children.Add(new Border { BorderBrush = FluentVisualResources.GetBrush("AccentFillColorDefaultBrush", Color.FromArgb(255, 0, 120, 212)), BorderThickness = new(focusPrimaryThickness), CornerRadius = new(cornerRadius.TopLeft + focusPrimaryThickness) });
        Grid focusVisual = new()
        {
            IsHitTestVisible = false,
            Margin = new(-focusOuterMargin),
            Visibility = Visibility.Collapsed
        };
        focusVisual.Children.Add(new Border { BorderBrush = FluentVisualResources.GetBrush("SystemControlFocusVisualPrimaryBrush", Color.FromArgb(255, 255, 255, 255)), BorderThickness = new(focusPrimaryThickness), CornerRadius = new(cornerRadius.TopLeft + focusOuterMargin) });
        focusVisual.Children.Add(new Border { BorderBrush = FluentVisualResources.GetBrush("SystemControlFocusVisualSecondaryBrush", Color.FromArgb(255, 0, 0, 0)), BorderThickness = new(focusSecondaryThickness), CornerRadius = new(cornerRadius.TopLeft + focusSecondaryMargin), Margin = new(focusSecondaryMargin) });
        Border backgroundHost = new()
        {
            Background = FluentVisualResources.GetBrush("CardBackgroundFillColorDefaultBrush", Color.FromArgb(255, 32, 32, 32)),
            CornerRadius = cornerRadius,
            IsHitTestVisible = false,
            Shadow = new ThemeShadow()
        };
        Grid compositionHost = new()
        {
            IsHitTestVisible = false
        };
        Grid content = new();
        content.Children.Add(compositionHost);
        Border host = new()
        {
            Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
            Child = content,
            CornerRadius = cornerRadius,
            IsHitTestVisible = false
        };
        Grid indicatorVisual = new();
        indicatorVisual.Children.Add(selectionVisual);
        indicatorVisual.Children.Add(focusVisual);
        Border focusHost = new()
        {
            Child = indicatorVisual,
            IsHitTestVisible = false
        };
        backgroundCanvas.Children.Add(backgroundHost);
        canvas.Children.Add(host);
        focusCanvas.Children.Add(focusHost);
        ThumbnailCompositionPreview? preview = ThumbnailCompositionPreview.Create(previewSurface, windowHandle, compositionHost, logger);
        if (preview is null)
        {
            backgroundHost.Background = new SolidColorBrush(Color.FromArgb(255, 32, 32, 32));
        }

        return new(windowHandle, host, backgroundHost, focusHost, preview, compositionHost, () => new(windowHandle, icons, visualScale, logger), focusVisual, selectionVisual, dragController, overviewDragScroller, dragPositionResolver, dragBoundaryCalculator, cursorConfinement, windowPlacementCoordinator, contextMenuBuilder, dragFrames, pager, throwPlacementResolver, pageLayouts, layoutScale);
    }
}

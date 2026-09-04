using Microsoft.Extensions.Logging;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Numerics;
using Windows.UI;

namespace Infinity.Shell.WinUI;

public sealed class ThumbnailCompositionPreview :
    IDisposable
{
    private const float CornerRadius = 8.0f;

    private readonly FrameworkElement host;
    private readonly WindowCapturePreview preview;
    private readonly SpriteVisual imageVisual;
    private readonly CompositionSurfaceBrush imageBrush;
    private readonly ContainerVisual rootVisual;
    private readonly SpriteVisual shadowVisual;
    private readonly CompositionColorBrush shadowSurfaceBrush;
    private readonly ShapeVisual shadowMaskVisual;
    private readonly CompositionSpriteShape shadowMaskShape;
    private readonly CompositionVisualSurface shadowMaskSurface;
    private readonly CompositionSurfaceBrush shadowMaskBrush;
    private readonly DropShadow shadow;
    private readonly CompositionRoundedRectangleGeometry shadowGeometry;
    private readonly CompositionRoundedRectangleGeometry roundedGeometry;
    private readonly CompositionGeometricClip roundedClip;
    private readonly ILogger logger;
    private bool isDisposed;
    private bool isVisible;
    private float width;
    private float height;

    private ThumbnailCompositionPreview(FrameworkElement host, WindowCapturePreview preview, SpriteVisual imageVisual, CompositionSurfaceBrush imageBrush, ContainerVisual rootVisual, SpriteVisual shadowVisual, CompositionColorBrush shadowSurfaceBrush, ShapeVisual shadowMaskVisual, CompositionSpriteShape shadowMaskShape, CompositionVisualSurface shadowMaskSurface, CompositionSurfaceBrush shadowMaskBrush, DropShadow shadow, CompositionRoundedRectangleGeometry shadowGeometry, CompositionRoundedRectangleGeometry roundedGeometry, CompositionGeometricClip roundedClip, ILogger logger)
    {
        this.host = host;
        this.preview = preview;
        this.imageVisual = imageVisual;
        this.imageBrush = imageBrush;
        this.rootVisual = rootVisual;
        this.shadowVisual = shadowVisual;
        this.shadowSurfaceBrush = shadowSurfaceBrush;
        this.shadowMaskVisual = shadowMaskVisual;
        this.shadowMaskShape = shadowMaskShape;
        this.shadowMaskSurface = shadowMaskSurface;
        this.shadowMaskBrush = shadowMaskBrush;
        this.shadow = shadow;
        this.shadowGeometry = shadowGeometry;
        this.roundedGeometry = roundedGeometry;
        this.roundedClip = roundedClip;
        this.logger = logger;
        preview.SurfaceChanged += HandleSurfaceChanged;
        preview.FrameAvailabilityChanged += HandleFrameAvailabilityChanged;
    }

    public static ThumbnailCompositionPreview? Create(WindowCapturePreviewSurface previewSurface, nint windowHandle, FrameworkElement host, ILogger logger)
    {
        if (!previewSurface.IsAvailable)
        {
            return null;
        }

        WindowCapturePreview? preview = previewSurface.CreatePreview(windowHandle);

        if (preview is null)
        {
            return null;
        }

        SpriteVisual? imageVisual = null;
        CompositionSurfaceBrush? imageBrush = null;
        ContainerVisual? rootVisual = null;
        SpriteVisual? shadowVisual = null;
        CompositionColorBrush? shadowSurfaceBrush = null;
        ShapeVisual? shadowMaskVisual = null;
        CompositionSpriteShape? shadowMaskShape = null;
        CompositionVisualSurface? shadowMaskSurface = null;
        CompositionSurfaceBrush? shadowMaskBrush = null;
        DropShadow? shadow = null;
        CompositionRoundedRectangleGeometry? shadowGeometry = null;
        CompositionRoundedRectangleGeometry? roundedGeometry = null;
        CompositionGeometricClip? roundedClip = null;
        try
        {
            Visual hostVisual = ElementCompositionPreview.GetElementVisual(host);
            Compositor compositor = hostVisual.Compositor;
            imageVisual = compositor.CreateSpriteVisual();
            imageVisual.IsVisible = false;
            imageBrush = compositor.CreateSurfaceBrush(preview.CreateSurface(compositor));
            imageBrush.Stretch = CompositionStretch.Fill;
            imageVisual.Brush = imageBrush;
            rootVisual = compositor.CreateContainerVisual();
            rootVisual.RelativeSizeAdjustment = Vector2.One;
            shadowVisual = compositor.CreateSpriteVisual();
            shadowSurfaceBrush = compositor.CreateColorBrush(Color.FromArgb(255, 32, 32, 32));
            shadowMaskVisual = compositor.CreateShapeVisual();
            shadowGeometry = compositor.CreateRoundedRectangleGeometry();
            shadowMaskShape = compositor.CreateSpriteShape(shadowGeometry);
            shadowMaskShape.FillBrush = shadowSurfaceBrush;
            shadowMaskVisual.Shapes.Add(shadowMaskShape);
            shadowMaskSurface = compositor.CreateVisualSurface();
            shadowMaskSurface.SourceVisual = shadowMaskVisual;
            shadowMaskBrush = compositor.CreateSurfaceBrush(shadowMaskSurface);
            shadow = compositor.CreateDropShadow();
            shadow.BlurRadius = 24;
            shadow.Color = Color.FromArgb(255, 0, 0, 0);
            shadow.Offset = new Vector3(0, 6, 0);
            shadow.Opacity = 0.45f;
            shadow.Mask = shadowMaskBrush;
            shadowVisual.Brush = shadowMaskBrush;
            shadowVisual.Shadow = shadow;
            roundedGeometry = compositor.CreateRoundedRectangleGeometry();
            roundedClip = compositor.CreateGeometricClip(roundedGeometry);
            imageVisual.Clip = roundedClip;
            rootVisual.Children.InsertAtBottom(shadowVisual);
            rootVisual.Children.InsertAtTop(imageVisual);

            ThumbnailCompositionPreview result = new(host, preview, imageVisual, imageBrush, rootVisual, shadowVisual, shadowSurfaceBrush, shadowMaskVisual, shadowMaskShape, shadowMaskSurface, shadowMaskBrush, shadow, shadowGeometry, roundedGeometry, roundedClip, logger);
            ElementCompositionPreview.SetElementChildVisual(host, rootVisual);
            return result;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to create the composition thumbnail preview");
            TryDetach(host, rootVisual, logger);
            TryRemove(imageVisual, logger);
            shadowVisual?.Dispose();
            shadowMaskBrush?.Dispose();
            shadowMaskSurface?.Dispose();
            shadowMaskShape?.Dispose();
            shadowMaskVisual?.Dispose();
            shadowGeometry?.Dispose();
            shadow?.Dispose();
            shadowSurfaceBrush?.Dispose();
            roundedClip?.Dispose();
            roundedGeometry?.Dispose();
            imageVisual?.Dispose();
            imageBrush?.Dispose();
            rootVisual?.Dispose();
            preview.Dispose();
            return null;
        }
    }

    public void Update(double width, double height, bool isVisible)
    {
        if (isDisposed)
        {
            return;
        }

        float normalizedWidth = NormalizeLength(width);
        float normalizedHeight = NormalizeLength(height);
        bool normalizedVisibility = isVisible && normalizedWidth > 0.0f && normalizedHeight > 0.0f;

        if (this.width == normalizedWidth &&
            this.height == normalizedHeight &&
            this.isVisible == normalizedVisibility)
        {
            return;
        }

        this.width = normalizedWidth;
        this.height = normalizedHeight;
        this.isVisible = normalizedVisibility;
        ApplyPresentation();
    }

    private void HandleSurfaceChanged()
    {
        if (isDisposed) return;
        try
        {
            imageBrush.Surface = preview.CreateSurface(imageVisual.Compositor);
            HandleFrameAvailabilityChanged();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Cannot reconnect the thumbnail surface for HWND {WindowHandle}", preview.WindowHandle);
        }
    }

    private void HandleFrameAvailabilityChanged()
    {
        if (isDisposed) return;
        imageVisual.IsVisible = isVisible && width > 0 && height > 0 && preview.HasCurrentFrame;
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        preview.SurfaceChanged -= HandleSurfaceChanged;
        preview.FrameAvailabilityChanged -= HandleFrameAvailabilityChanged;
        preview.SetVisible(false);
        TryDetach(host, rootVisual, logger);
        TryRemove(imageVisual, logger);
        TryRemove(shadowVisual, logger);
        imageVisual.Clip = null;
        imageVisual.Brush = null;
        imageBrush.Surface = null;
        shadowVisual.Shadow = null;
        shadowVisual.Brush = null;
        shadow.Mask = null;
        shadowMaskSurface.SourceVisual = null;
        shadowMaskBrush.Dispose();
        shadowMaskSurface.Dispose();
        shadowMaskShape.FillBrush = null;
        shadowMaskShape.Dispose();
        shadowMaskVisual.Dispose();
        shadowGeometry.Dispose();
        shadow.Dispose();
        shadowSurfaceBrush.Dispose();
        shadowVisual.Dispose();
        roundedClip.Dispose();
        roundedGeometry.Dispose();
        imageVisual.Dispose();
        imageBrush.Dispose();
        rootVisual.Dispose();
        preview.Dispose();
        GC.SuppressFinalize(this);
    }

    private void ApplyPresentation()
    {
        if (width <= 0.0f || height <= 0.0f)
        {
            imageVisual.IsVisible = false;
            preview.SetVisible(false);
            return;
        }

        preview.SetVisible(isVisible);
        imageVisual.Size = new Vector2(width, height);
        imageVisual.IsVisible = isVisible && preview.HasCurrentFrame;
        shadowVisual.Size = new Vector2(width, height);
        shadowVisual.IsVisible = isVisible;
        shadowMaskVisual.Size = new Vector2(width, height);
        shadowMaskSurface.SourceSize = new Vector2(width, height);
        shadowGeometry.Size = new Vector2(width, height);
        shadowGeometry.CornerRadius = new Vector2(MathF.Min(CornerRadius, width / 2), MathF.Min(CornerRadius, height / 2));
        UpdateClip(width, height);
    }

    private void UpdateClip(float width, float height)
    {
        float horizontalRadius = MathF.Min(CornerRadius, width / 2.0f);
        float verticalRadius = MathF.Min(CornerRadius, height / 2.0f);

        roundedGeometry.Size = new Vector2(width, height);
        roundedGeometry.CornerRadius = new Vector2(horizontalRadius, verticalRadius);
    }

    private static void TryDetach(FrameworkElement host, Visual? visual, ILogger logger)
    {
        if (visual is null)
        {
            return;
        }

        try
        {
            if (ReferenceEquals(ElementCompositionPreview.GetElementChildVisual(host), visual))
            {
                ElementCompositionPreview.SetElementChildVisual(host, null);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to detach the composition thumbnail preview");
        }
    }

    private static void TryRemove(Visual? visual, ILogger logger)
    {
        if (visual is null)
        {
            return;
        }

        try
        {
            if (visual.Parent is ContainerVisual parent)
            {
                parent.Children.Remove(visual);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to remove the composition thumbnail visual");
        }
    }

    private static float NormalizeLength(double value)
    {
        if (!double.IsFinite(value) || value <= 0.0)
        {
            return 0.0f;
        }

        return (float)Math.Min(value, float.MaxValue);
    }

}

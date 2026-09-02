using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using System;
using System.Numerics;
using Windows.UI;

namespace Infinity.Shell.WinUI;

public sealed class ThumbnailCompositionPreview :
    IDisposable
{
    private const float CornerRadius = 8.0f;

    private readonly FrameworkElement transformSource;
    private readonly DesktopThumbnailCompositionLayer layer;
    private readonly IWindowPreview preview;
    private readonly SystemVisualProxyVisualPrivate proxy;
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
    private float rasterScale = 1.0f;
    private float width;
    private float height;
    private TimeSpan? translationTransitionDuration;
    private TimeSpan? scaleTransitionDuration;

    private ThumbnailCompositionPreview(FrameworkElement transformSource, DesktopThumbnailCompositionLayer layer, IWindowPreview preview, SystemVisualProxyVisualPrivate proxy, ContainerVisual rootVisual, SpriteVisual shadowVisual, CompositionColorBrush shadowSurfaceBrush, ShapeVisual shadowMaskVisual, CompositionSpriteShape shadowMaskShape, CompositionVisualSurface shadowMaskSurface, CompositionSurfaceBrush shadowMaskBrush, DropShadow shadow, CompositionRoundedRectangleGeometry shadowGeometry, CompositionRoundedRectangleGeometry roundedGeometry, CompositionGeometricClip roundedClip, ILogger logger)
    {
        this.transformSource = transformSource;
        this.layer = layer;
        this.preview = preview;
        this.proxy = proxy;
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
    }

    internal ContainerVisual RootVisual => rootVisual;

    public static ThumbnailCompositionPreview? Create(IWindowPreviewSurface previewSurface, nint windowHandle, FrameworkElement transformSource, DesktopThumbnailCompositionLayer layer, ILogger logger)
    {
        if (!previewSurface.IsAvailable || layer.Compositor is not Compositor compositor)
        {
            return null;
        }

        IWindowPreview? preview = previewSurface.CreatePreview(windowHandle);

        if (preview is null)
        {
            return null;
        }

        SystemVisualProxyVisualPrivate? proxy = null;
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
        ThumbnailCompositionPreview? result = null;

        try
        {
            proxy = SystemVisualProxyVisualPrivate.Create(compositor);
            rootVisual = compositor.CreateContainerVisual();
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
            proxy.Visual.Clip = roundedClip;
            rootVisual.Children.InsertAtBottom(shadowVisual);
            rootVisual.Children.InsertAtTop(proxy.Visual);

            result = new ThumbnailCompositionPreview(transformSource, layer, preview, proxy, rootVisual, shadowVisual, shadowSurfaceBrush, shadowMaskVisual, shadowMaskShape, shadowMaskSurface, shadowMaskBrush, shadow, shadowGeometry, roundedGeometry, roundedClip, logger);
            layer.Add(result);
            return result;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to create the composition thumbnail preview");
            if (result is not null)
            {
                layer.Remove(result);
            }

            TryRemove(proxy?.Visual, logger);
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
            proxy?.Dispose();
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
        float normalizedRasterScale = NormalizeScale(transformSource.XamlRoot?.RasterizationScale ?? 1.0);
        bool normalizedVisibility = isVisible && normalizedWidth > 0.0f && normalizedHeight > 0.0f;

        if (this.width == normalizedWidth &&
            this.height == normalizedHeight &&
            rasterScale == normalizedRasterScale &&
            this.isVisible == normalizedVisibility)
        {
            return;
        }

        this.width = normalizedWidth;
        this.height = normalizedHeight;
        rasterScale = normalizedRasterScale;
        this.isVisible = normalizedVisibility;
        ApplyPresentation();
    }

    public void RefreshSource()
    {
        if (!isDisposed)
        {
            preview.RefreshSource();
        }
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        preview.SetTarget(0, 0.0, 0.0, false);
        layer.Remove(this);
        rootVisual.StopAnimation(nameof(Visual.Offset));
        rootVisual.StopAnimation(nameof(Visual.Scale));
        rootVisual.StopAnimation(nameof(Visual.CenterPoint));
        rootVisual.StopAnimation(nameof(Visual.Opacity));
        TryRemove(proxy.Visual, logger);
        TryRemove(shadowVisual, logger);
        proxy.Visual.Clip = null;
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
        proxy.Dispose();
        rootVisual.Dispose();
        preview.Dispose();
        GC.SuppressFinalize(this);
    }

    public void SetZIndex(int value)
    {
        if (!isDisposed)
        {
            layer.SetZIndex(this, value);
        }
    }

    public void SetTranslationTransition(TimeSpan? duration) => translationTransitionDuration = duration;

    public void SetScaleTransition(TimeSpan? duration) => scaleTransitionDuration = duration;

    public void SetOffset(Vector3 value)
    {
        if (isDisposed)
        {
            return;
        }

        SetVector3(nameof(Visual.Offset), value, translationTransitionDuration);
    }

    public void SetScale(Vector3 value)
    {
        if (!isDisposed)
        {
            SetVector3(nameof(Visual.Scale), value, scaleTransitionDuration);
        }
    }

    public void SetCenterPoint(Vector3 value)
    {
        if (!isDisposed)
        {
            rootVisual.StopAnimation(nameof(Visual.CenterPoint));
            rootVisual.CenterPoint = value;
        }
    }

    public void SetOpacity(float value)
    {
        if (!isDisposed)
        {
            rootVisual.StopAnimation(nameof(Visual.Opacity));
            rootVisual.Opacity = Math.Clamp(value, 0, 1);
        }
    }

    private void SetVector3(string property, Vector3 value, TimeSpan? duration)
    {
        if (!duration.HasValue)
        {
            rootVisual.StopAnimation(property);
            SetVector3Value(property, value);
            return;
        }

        Compositor compositor = rootVisual.Compositor;
        Vector3KeyFrameAnimation animation = compositor.CreateVector3KeyFrameAnimation();
        CubicBezierEasingFunction easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1));
        animation.Duration = duration.Value;
        animation.InsertExpressionKeyFrame(0, "this.StartingValue");
        animation.InsertKeyFrame(1, value, easing);
        rootVisual.StartAnimation(property, animation);
        easing.Dispose();
        animation.Dispose();
    }

    private void SetVector3Value(string property, Vector3 value)
    {
        if (property == nameof(Visual.Offset))
        {
            rootVisual.Offset = value;
        }
        else if (property == nameof(Visual.Scale))
        {
            rootVisual.Scale = value;
        }
    }

    private void ApplyPresentation()
    {
        if (width <= 0.0f || height <= 0.0f)
        {
            proxy.Visual.IsVisible = false;
            preview.SetTarget(0, 0.0, 0.0, false);
            return;
        }

        float renderWidth = MathF.Max(1.0f, MathF.Round(width * rasterScale));
        float renderHeight = MathF.Max(1.0f, MathF.Round(height * rasterScale));
        float horizontalScale = width / renderWidth;
        float verticalScale = height / renderHeight;

        preview.SetTarget(proxy.Handle, renderWidth, renderHeight, isVisible);
        proxy.Visual.Size = new Vector2(renderWidth, renderHeight);
        proxy.Visual.Scale = new Vector3(horizontalScale, verticalScale, 1.0f);
        proxy.Visual.IsVisible = isVisible;
        shadowVisual.Size = new Vector2(width, height);
        shadowVisual.IsVisible = isVisible;
        shadowMaskVisual.Size = new Vector2(width, height);
        shadowMaskSurface.SourceSize = new Vector2(width, height);
        shadowGeometry.Size = new Vector2(width, height);
        shadowGeometry.CornerRadius = new Vector2(MathF.Min(CornerRadius, width / 2), MathF.Min(CornerRadius, height / 2));
        UpdateClip(renderWidth, renderHeight, horizontalScale, verticalScale);
    }

    private void UpdateClip(float width, float height, float horizontalScale, float verticalScale)
    {
        float horizontalRadius = MathF.Min(CornerRadius / horizontalScale, width / 2.0f);
        float verticalRadius = MathF.Min(CornerRadius / verticalScale, height / 2.0f);

        roundedGeometry.Size = new Vector2(width, height);
        roundedGeometry.CornerRadius = new Vector2(horizontalRadius, verticalRadius);
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

    private static float NormalizeScale(double value) => double.IsFinite(value) && value > 0.0 ? (float)Math.Min(value, float.MaxValue) : 1.0f;
}

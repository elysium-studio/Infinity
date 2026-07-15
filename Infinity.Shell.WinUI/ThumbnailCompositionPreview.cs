using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Numerics;

namespace Infinity.Shell.WinUI;

public class ThumbnailCompositionPreview :
    IDisposable
{
    private const float CornerRadius = 8.0f;

    private readonly FrameworkElement host;
    private readonly IWindowPreview preview;
    private readonly SystemVisualProxyVisualPrivate proxy;
    private readonly CompositionVisualSurface visualSurface;
    private readonly CompositionSurfaceBrush surfaceBrush;
    private readonly CompositionRoundedRectangleGeometry roundedGeometry;
    private readonly CompositionGeometricClip roundedClip;
    private readonly SpriteVisual spriteVisual;
    private readonly ILogger logger;
    private bool isDisposed;
    private bool hasUpdate;
    private bool isVisible;
    private float width;
    private float height;

    private ThumbnailCompositionPreview(FrameworkElement host,
        IWindowPreview preview,
        SystemVisualProxyVisualPrivate proxy,
        CompositionVisualSurface visualSurface,
        CompositionSurfaceBrush surfaceBrush,
        CompositionRoundedRectangleGeometry roundedGeometry,
        CompositionGeometricClip roundedClip,
        SpriteVisual spriteVisual,
        ILogger logger)
    {
        this.host = host;
        this.preview = preview;
        this.proxy = proxy;
        this.visualSurface = visualSurface;
        this.surfaceBrush = surfaceBrush;
        this.roundedGeometry = roundedGeometry;
        this.roundedClip = roundedClip;
        this.spriteVisual = spriteVisual;
        this.logger = logger;
    }

    public static ThumbnailCompositionPreview? Create(IWindowPreviewSurface previewSurface,
        nint windowHandle,
        FrameworkElement host,
        ILogger logger)
    {
        if (!previewSurface.IsAvailable)
        {
            return null;
        }

        IWindowPreview? preview = previewSurface.CreatePreview(windowHandle);

        if (preview is null)
        {
            return null;
        }

        SystemVisualProxyVisualPrivate? proxy = null;
        CompositionVisualSurface? visualSurface = null;
        CompositionSurfaceBrush? surfaceBrush = null;
        CompositionRoundedRectangleGeometry? roundedGeometry = null;
        CompositionGeometricClip? roundedClip = null;
        SpriteVisual? spriteVisual = null;

        try
        {
            Visual hostVisual = ElementCompositionPreview.GetElementVisual(host);
            Compositor compositor = hostVisual.Compositor;
            proxy = SystemVisualProxyVisualPrivate.Create(compositor);
            visualSurface = compositor.CreateVisualSurface();
            visualSurface.SourceVisual = proxy.Visual;
            visualSurface.SourceOffset = Vector2.Zero;

            surfaceBrush = compositor.CreateSurfaceBrush(visualSurface);
            surfaceBrush.Stretch = CompositionStretch.Fill;

            roundedGeometry = compositor.CreateRoundedRectangleGeometry();
            roundedClip = compositor.CreateGeometricClip(roundedGeometry);

            spriteVisual = compositor.CreateSpriteVisual();
            spriteVisual.Brush = surfaceBrush;
            spriteVisual.Clip = roundedClip;
            spriteVisual.RelativeSizeAdjustment = Vector2.One;
            ElementCompositionPreview.SetElementChildVisual(host, spriteVisual);

            return new ThumbnailCompositionPreview(host,
                preview,
                proxy,
                visualSurface,
                surfaceBrush,
                roundedGeometry,
                roundedClip,
                spriteVisual,
                logger);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to create the composition thumbnail preview");
            TryDetach(host, spriteVisual, logger);
            spriteVisual?.Dispose();
            roundedClip?.Dispose();
            roundedGeometry?.Dispose();
            surfaceBrush?.Dispose();
            visualSurface?.Dispose();
            proxy?.Dispose();
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

        if (hasUpdate &&
            this.width == normalizedWidth &&
            this.height == normalizedHeight &&
            this.isVisible == normalizedVisibility)
        {
            return;
        }

        hasUpdate = true;
        this.width = normalizedWidth;
        this.height = normalizedHeight;
        this.isVisible = normalizedVisibility;
        proxy.Visual.Size = new Vector2(normalizedWidth, normalizedHeight);
        visualSurface.SourceSize = new Vector2(normalizedWidth, normalizedHeight);
        spriteVisual.IsVisible = normalizedVisibility;
        UpdateClip(normalizedWidth, normalizedHeight);
        preview.SetTarget(proxy.Handle, normalizedWidth, normalizedHeight, normalizedVisibility);
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        preview.SetTarget(0, 0.0, 0.0, false);
        TryDetach(host, spriteVisual, logger);
        spriteVisual.Brush = null;
        spriteVisual.Clip = null;
        visualSurface.SourceVisual = null;
        spriteVisual.Dispose();
        roundedClip.Dispose();
        roundedGeometry.Dispose();
        surfaceBrush.Dispose();
        visualSurface.Dispose();
        proxy.Dispose();
        preview.Dispose();
        GC.SuppressFinalize(this);
    }

    private static void TryDetach(FrameworkElement host, SpriteVisual? spriteVisual, ILogger logger)
    {
        if (spriteVisual is null)
        {
            return;
        }

        try
        {
            if (ReferenceEquals(ElementCompositionPreview.GetElementChildVisual(host), spriteVisual))
            {
                ElementCompositionPreview.SetElementChildVisual(host, null);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to detach the composition thumbnail preview");
        }
    }

    private void UpdateClip(float width, float height)
    {
        float radius = MathF.Min(CornerRadius, MathF.Min(width, height) / 2.0f);
        roundedGeometry.Size = new Vector2(width, height);
        roundedGeometry.CornerRadius = new Vector2(radius, radius);
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

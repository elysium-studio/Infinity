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
    private readonly ContainerVisual hostContainer;
    private readonly CompositionRoundedRectangleGeometry roundedGeometry;
    private readonly CompositionGeometricClip roundedClip;
    private readonly ILogger logger;
    private ContainerVisual? dragContainer;
    private FrameworkElement? dragHost;
    private CompositionVisualSurface? dragSurface;
    private CompositionSurfaceBrush? dragBrush;
    private SpriteVisual? dragVisual;
    private CompositionRoundedRectangleGeometry? dragRoundedGeometry;
    private CompositionGeometricClip? dragRoundedClip;
    private Vector2 dragOrigin;
    private bool isDisposed;
    private bool isVisible;
    private float width;
    private float height;

    private ThumbnailCompositionPreview(FrameworkElement host,
        IWindowPreview preview,
        SystemVisualProxyVisualPrivate proxy,
        ContainerVisual hostContainer,
        CompositionRoundedRectangleGeometry roundedGeometry,
        CompositionGeometricClip roundedClip,
        ILogger logger)
    {
        this.host = host;
        this.preview = preview;
        this.proxy = proxy;
        this.hostContainer = hostContainer;
        this.roundedGeometry = roundedGeometry;
        this.roundedClip = roundedClip;
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
        ContainerVisual? hostContainer = null;
        CompositionRoundedRectangleGeometry? roundedGeometry = null;
        CompositionGeometricClip? roundedClip = null;

        try
        {
            Visual hostVisual = ElementCompositionPreview.GetElementVisual(host);
            Compositor compositor = hostVisual.Compositor;
            proxy = SystemVisualProxyVisualPrivate.Create(compositor);
            hostContainer = compositor.CreateContainerVisual();
            hostContainer.RelativeSizeAdjustment = Vector2.One;
            roundedGeometry = compositor.CreateRoundedRectangleGeometry();
            roundedClip = compositor.CreateGeometricClip(roundedGeometry);
            proxy.Visual.Clip = roundedClip;
            hostContainer.Children.InsertAtTop(proxy.Visual);
            ElementCompositionPreview.SetElementChildVisual(host, hostContainer);

            return new ThumbnailCompositionPreview(host,
                preview,
                proxy,
                hostContainer,
                roundedGeometry,
                roundedClip,
                logger);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to create the composition thumbnail preview");
            TryDetach(host, hostContainer, logger);
            TryRemove(proxy?.Visual, logger);
            roundedClip?.Dispose();
            roundedGeometry?.Dispose();
            proxy?.Dispose();
            hostContainer?.Dispose();
            preview.Dispose();
            return null;
        }
    }

    public bool BeginDrag(FrameworkElement overlayHost)
    {
        if (isDisposed || dragContainer is not null || width <= 0.0f || height <= 0.0f)
        {
            return false;
        }

        ContainerVisual? createdContainer = null;
        CompositionVisualSurface? createdSurface = null;
        CompositionSurfaceBrush? createdBrush = null;
        SpriteVisual? createdVisual = null;
        CompositionRoundedRectangleGeometry? createdRoundedGeometry = null;
        CompositionGeometricClip? createdRoundedClip = null;

        try
        {
            if (!ReferenceEquals(ElementCompositionPreview.GetElementChildVisual(host), hostContainer) ||
                ElementCompositionPreview.GetElementChildVisual(overlayHost) is not null)
            {
                return false;
            }

            Visual overlayVisual = ElementCompositionPreview.GetElementVisual(overlayHost);
            Windows.Foundation.Point position = host.TransformToVisual(overlayHost).TransformPoint(default);

            if (!double.IsFinite(position.X) || !double.IsFinite(position.Y))
            {
                return false;
            }

            createdContainer = overlayVisual.Compositor.CreateContainerVisual();
            createdContainer.RelativeSizeAdjustment = Vector2.One;
            createdSurface = overlayVisual.Compositor.CreateVisualSurface();
            createdSurface.SourceVisual = proxy.Visual;
            createdSurface.SourceSize = new Vector2(width, height);
            createdBrush = overlayVisual.Compositor.CreateSurfaceBrush(createdSurface);
            createdVisual = overlayVisual.Compositor.CreateSpriteVisual();
            createdVisual.Size = new Vector2(width, height);
            createdVisual.Brush = createdBrush;
            createdRoundedGeometry = overlayVisual.Compositor.CreateRoundedRectangleGeometry();
            createdRoundedGeometry.Size = new Vector2(width, height);
            float radius = MathF.Min(CornerRadius, MathF.Min(width, height) / 2.0f);
            createdRoundedGeometry.CornerRadius = new Vector2(radius, radius);
            createdRoundedClip = overlayVisual.Compositor.CreateGeometricClip(createdRoundedGeometry);
            createdVisual.Clip = createdRoundedClip;
            ElementCompositionPreview.SetElementChildVisual(overlayHost, createdContainer);
            dragOrigin = new Vector2((float)position.X, (float)position.Y);
            createdVisual.Offset = new Vector3(dragOrigin, 0.0f);
            createdContainer.Children.InsertAtTop(createdVisual);
            dragHost = overlayHost;
            dragContainer = createdContainer;
            dragSurface = createdSurface;
            dragBrush = createdBrush;
            dragVisual = createdVisual;
            dragRoundedGeometry = createdRoundedGeometry;
            dragRoundedClip = createdRoundedClip;
            return true;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to create the thumbnail drag surface");
            TryRemove(createdVisual, logger);
            TryDetach(overlayHost, createdContainer, logger);
            if (createdVisual is not null)
            {
                createdVisual.Brush = null;
                createdVisual.Clip = null;
            }

            if (createdSurface is not null)
            {
                createdSurface.SourceVisual = null;
            }

            createdRoundedClip?.Dispose();
            createdRoundedGeometry?.Dispose();
            createdVisual?.Dispose();
            createdBrush?.Dispose();
            createdSurface?.Dispose();
            createdContainer?.Dispose();
            return false;
        }
    }

    public void MoveDrag(double horizontalDelta, double verticalDelta)
    {
        if (isDisposed || dragVisual is null ||
            !double.IsFinite(horizontalDelta) || !double.IsFinite(verticalDelta))
        {
            return;
        }

        float x = ClampToFloat(dragOrigin.X + horizontalDelta);
        float y = ClampToFloat(dragOrigin.Y + verticalDelta);
        dragVisual.Offset = new Vector3(x, y, 0.0f);
    }

    public void EndDrag()
    {
        ContainerVisual? currentDragContainer = dragContainer;
        FrameworkElement? currentDragHost = dragHost;
        CompositionVisualSurface? currentDragSurface = dragSurface;
        CompositionSurfaceBrush? currentDragBrush = dragBrush;
        SpriteVisual? currentDragVisual = dragVisual;
        CompositionRoundedRectangleGeometry? currentDragRoundedGeometry = dragRoundedGeometry;
        CompositionGeometricClip? currentDragRoundedClip = dragRoundedClip;

        if (currentDragContainer is null || currentDragHost is null)
        {
            return;
        }

        dragContainer = null;
        dragHost = null;
        dragSurface = null;
        dragBrush = null;
        dragVisual = null;
        dragRoundedGeometry = null;
        dragRoundedClip = null;

        try
        {
            if (currentDragVisual is not null)
            {
                currentDragContainer.Children.Remove(currentDragVisual);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to remove the thumbnail drag visual");
            TryRemove(currentDragVisual, logger);
        }
        finally
        {
            TryDetach(currentDragHost, currentDragContainer, logger);
            if (currentDragVisual is not null)
            {
                currentDragVisual.Brush = null;
                currentDragVisual.Clip = null;
            }

            if (currentDragSurface is not null)
            {
                currentDragSurface.SourceVisual = null;
            }

            currentDragRoundedClip?.Dispose();
            currentDragRoundedGeometry?.Dispose();
            currentDragVisual?.Dispose();
            currentDragBrush?.Dispose();
            currentDragSurface?.Dispose();
            currentDragContainer.Dispose();
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
        proxy.Visual.Size = new Vector2(normalizedWidth, normalizedHeight);
        UpdateClip(normalizedWidth, normalizedHeight);
        proxy.Visual.IsVisible = normalizedVisibility;
        UpdateDragSurface(normalizedWidth, normalizedHeight);
        preview.SetTarget(proxy.Handle, normalizedWidth, normalizedHeight, normalizedVisibility);
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        EndDrag();
        preview.SetTarget(0, 0.0, 0.0, false);
        TryDetach(host, hostContainer, logger);
        TryRemove(proxy.Visual, logger);
        proxy.Visual.Clip = null;
        roundedClip.Dispose();
        roundedGeometry.Dispose();
        proxy.Dispose();
        hostContainer.Dispose();
        preview.Dispose();
        GC.SuppressFinalize(this);
    }

    private void UpdateClip(float width, float height)
    {
        float radius = MathF.Min(CornerRadius, MathF.Min(width, height) / 2.0f);
        roundedGeometry.Size = new Vector2(width, height);
        roundedGeometry.CornerRadius = new Vector2(radius, radius);
    }

    private void UpdateDragSurface(float width, float height)
    {
        if (dragSurface is null || dragVisual is null || dragRoundedGeometry is null)
        {
            return;
        }

        Vector2 size = new(width, height);
        dragSurface.SourceSize = size;
        dragVisual.Size = size;
        dragRoundedGeometry.Size = size;
        float radius = MathF.Min(CornerRadius, MathF.Min(width, height) / 2.0f);
        dragRoundedGeometry.CornerRadius = new Vector2(radius, radius);
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

    private static float ClampToFloat(double value) =>
        (float)Math.Clamp(value, -float.MaxValue, float.MaxValue);

    private static float NormalizeLength(double value)
    {
        if (!double.IsFinite(value) || value <= 0.0)
        {
            return 0.0f;
        }

        return (float)Math.Min(value, float.MaxValue);
    }
}

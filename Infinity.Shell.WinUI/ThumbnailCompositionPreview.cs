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

    private readonly IWindowPreviewSurface previewSurface;
    private readonly nint windowHandle;
    private readonly FrameworkElement host;
    private readonly IWindowPreview preview;
    private readonly SystemVisualProxyVisualPrivate proxy;
    private readonly ContainerVisual hostContainer;
    private readonly CompositionRoundedRectangleGeometry roundedGeometry;
    private readonly CompositionGeometricClip roundedClip;
    private readonly ILogger logger;
    private ContainerVisual? dragContainer;
    private CompositionGeometricClip? dragRoundedClip;
    private CompositionRoundedRectangleGeometry? dragRoundedGeometry;
    private FrameworkElement? dragHost;
    private IWindowPreview? dragPreview;
    private SystemVisualProxyVisualPrivate? dragProxy;
    private Vector2 dragOrigin;
    private bool isDisposed;
    private bool isVisible;
    private float width;
    private float height;

    private ThumbnailCompositionPreview(IWindowPreviewSurface previewSurface,
        nint windowHandle,
        FrameworkElement host,
        IWindowPreview preview,
        SystemVisualProxyVisualPrivate proxy,
        ContainerVisual hostContainer,
        CompositionRoundedRectangleGeometry roundedGeometry,
        CompositionGeometricClip roundedClip,
        ILogger logger)
    {
        this.previewSurface = previewSurface;
        this.windowHandle = windowHandle;
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

            return new ThumbnailCompositionPreview(previewSurface,
                windowHandle,
                host,
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
        if (isDisposed || dragPreview is not null)
        {
            return false;
        }

        ContainerVisual? createdContainer = null;
        CompositionGeometricClip? createdClip = null;
        CompositionRoundedRectangleGeometry? createdGeometry = null;
        IWindowPreview? createdPreview = null;
        SystemVisualProxyVisualPrivate? createdProxy = null;

        try
        {
            if (!ReferenceEquals(ElementCompositionPreview.GetElementChildVisual(host), hostContainer) ||
                ElementCompositionPreview.GetElementChildVisual(overlayHost) is not null)
            {
                return false;
            }

            Visual overlayVisual = ElementCompositionPreview.GetElementVisual(overlayHost);
            Compositor compositor = overlayVisual.Compositor;
            Windows.Foundation.Point position = host.TransformToVisual(overlayHost).TransformPoint(default);

            if (!double.IsFinite(position.X) || !double.IsFinite(position.Y))
            {
                return false;
            }

            createdPreview = previewSurface.CreatePreview(windowHandle);

            if (createdPreview is null)
            {
                return false;
            }

            createdProxy = SystemVisualProxyVisualPrivate.Create(compositor);
            createdGeometry = compositor.CreateRoundedRectangleGeometry();
            createdClip = compositor.CreateGeometricClip(createdGeometry);
            createdProxy.Visual.Size = new Vector2(width, height);
            createdProxy.Visual.Clip = createdClip;
            createdProxy.Visual.IsVisible = isVisible;
            UpdateClip(createdGeometry, width, height);

            createdContainer = compositor.CreateContainerVisual();
            createdContainer.RelativeSizeAdjustment = Vector2.One;
            dragOrigin = new Vector2((float)position.X, (float)position.Y);
            createdProxy.Visual.Offset = new Vector3(dragOrigin, 0.0f);
            createdContainer.Children.InsertAtTop(createdProxy.Visual);
            ElementCompositionPreview.SetElementChildVisual(overlayHost, createdContainer);

            using (previewSurface.DeferUpdates())
            {
                preview.SetTarget(proxy.Handle, width, height, false);
                createdPreview.SetTarget(createdProxy.Handle, width, height, isVisible);
            }

            dragHost = overlayHost;
            dragContainer = createdContainer;
            dragRoundedClip = createdClip;
            dragRoundedGeometry = createdGeometry;
            dragPreview = createdPreview;
            dragProxy = createdProxy;
            return true;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to elevate the live thumbnail for dragging");

            if (createdPreview is not null)
            {
                using (previewSurface.DeferUpdates())
                {
                    preview.SetTarget(proxy.Handle, width, height, isVisible);
                    createdPreview.Dispose();
                }
            }

            TryDetach(overlayHost, createdContainer, logger);
            TryRemove(createdProxy?.Visual, logger);

            if (createdProxy is not null)
            {
                createdProxy.Visual.Clip = null;
            }

            createdClip?.Dispose();
            createdGeometry?.Dispose();
            createdProxy?.Dispose();
            createdContainer?.Dispose();
            return false;
        }
    }

    public void MoveDrag(double horizontalDelta, double verticalDelta)
    {
        SystemVisualProxyVisualPrivate? currentDragProxy = dragProxy;

        if (isDisposed || currentDragProxy is null ||
            !double.IsFinite(horizontalDelta) || !double.IsFinite(verticalDelta))
        {
            return;
        }

        float x = ClampToFloat(dragOrigin.X + horizontalDelta);
        float y = ClampToFloat(dragOrigin.Y + verticalDelta);
        currentDragProxy.Visual.Offset = new Vector3(x, y, 0.0f);
    }

    public void EndDrag()
    {
        ContainerVisual? currentDragContainer = dragContainer;
        CompositionGeometricClip? currentDragClip = dragRoundedClip;
        CompositionRoundedRectangleGeometry? currentDragGeometry = dragRoundedGeometry;
        FrameworkElement? currentDragHost = dragHost;
        IWindowPreview? currentDragPreview = dragPreview;
        SystemVisualProxyVisualPrivate? currentDragProxy = dragProxy;

        if (currentDragContainer is null || currentDragHost is null ||
            currentDragPreview is null || currentDragProxy is null)
        {
            return;
        }

        dragContainer = null;
        dragRoundedClip = null;
        dragRoundedGeometry = null;
        dragHost = null;
        dragPreview = null;
        dragProxy = null;

        try
        {
            using (previewSurface.DeferUpdates())
            {
                preview.SetTarget(proxy.Handle, width, height, isVisible);
                currentDragPreview.Dispose();
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to restore the live thumbnail after dragging");
        }
        finally
        {
            TryDetach(currentDragHost, currentDragContainer, logger);
            TryRemove(currentDragProxy.Visual, logger);
            currentDragProxy.Visual.Clip = null;
            currentDragClip?.Dispose();
            currentDragGeometry?.Dispose();
            currentDragProxy.Dispose();
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
        UpdateClip(roundedGeometry, normalizedWidth, normalizedHeight);
        proxy.Visual.IsVisible = normalizedVisibility;

        if (dragPreview is not null && dragProxy is not null)
        {
            dragProxy.Visual.Size = new Vector2(normalizedWidth, normalizedHeight);
            dragProxy.Visual.IsVisible = normalizedVisibility;

            if (dragRoundedGeometry is not null)
            {
                UpdateClip(dragRoundedGeometry, normalizedWidth, normalizedHeight);
            }

            using (previewSurface.DeferUpdates())
            {
                preview.SetTarget(proxy.Handle, normalizedWidth, normalizedHeight, false);
                dragPreview.SetTarget(dragProxy.Handle,
                    normalizedWidth,
                    normalizedHeight,
                    normalizedVisibility);
            }
        }
        else
        {
            preview.SetTarget(proxy.Handle, normalizedWidth, normalizedHeight, normalizedVisibility);
        }
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

    private static void UpdateClip(CompositionRoundedRectangleGeometry geometry,
        float width,
        float height)
    {
        float radius = MathF.Min(CornerRadius, MathF.Min(width, height) / 2.0f);
        geometry.Size = new Vector2(width, height);
        geometry.CornerRadius = new Vector2(radius, radius);
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

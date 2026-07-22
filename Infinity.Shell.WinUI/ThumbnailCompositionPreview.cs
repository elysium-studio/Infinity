using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Numerics;

namespace Infinity.Shell.WinUI;

public sealed class ThumbnailCompositionPreview :
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
        if (isDisposed || dragContainer is not null)
        {
            return false;
        }

        ContainerVisual? createdContainer = null;

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
            ElementCompositionPreview.SetElementChildVisual(overlayHost, createdContainer);
            hostContainer.Children.Remove(proxy.Visual);
            dragOrigin = new Vector2((float)position.X, (float)position.Y);
            proxy.Visual.Offset = new Vector3(dragOrigin, 0.0f);
            createdContainer.Children.InsertAtTop(proxy.Visual);
            dragHost = overlayHost;
            dragContainer = createdContainer;
            return true;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to elevate the live thumbnail for dragging");
            TryRemove(proxy.Visual, logger);
            proxy.Visual.Offset = Vector3.Zero;
            TryInsert(hostContainer, proxy.Visual, logger);
            TryDetach(overlayHost, createdContainer, logger);
            createdContainer?.Dispose();
            return false;
        }
    }

    public void MoveDrag(double horizontalDelta, double verticalDelta)
    {
        if (isDisposed || dragContainer is null ||
            !double.IsFinite(horizontalDelta) || !double.IsFinite(verticalDelta))
        {
            return;
        }

        float x = ClampToFloat(dragOrigin.X + horizontalDelta);
        float y = ClampToFloat(dragOrigin.Y + verticalDelta);
        proxy.Visual.Offset = new Vector3(x, y, 0.0f);
    }

    public void EndDrag()
    {
        ContainerVisual? currentDragContainer = dragContainer;
        FrameworkElement? currentDragHost = dragHost;

        if (currentDragContainer is null || currentDragHost is null)
        {
            return;
        }

        dragContainer = null;
        dragHost = null;

        try
        {
            currentDragContainer.Children.Remove(proxy.Visual);
            proxy.Visual.Offset = Vector3.Zero;
            hostContainer.Children.InsertAtTop(proxy.Visual);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to restore the live thumbnail after dragging");
            TryRemove(proxy.Visual, logger);
            proxy.Visual.Offset = Vector3.Zero;
            TryInsert(hostContainer, proxy.Visual, logger);
        }
        finally
        {
            TryDetach(currentDragHost, currentDragContainer, logger);
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

    private static void TryInsert(ContainerVisual container, Visual visual, ILogger logger)
    {
        try
        {
            container.Children.InsertAtTop(visual);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to attach the composition thumbnail visual");
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

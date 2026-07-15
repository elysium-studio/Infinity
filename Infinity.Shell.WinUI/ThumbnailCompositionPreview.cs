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
    private readonly FrameworkElement host;
    private readonly IWindowPreview preview;
    private readonly SystemVisualProxyVisualPrivate proxy;
    private readonly ILogger logger;
    private bool isDisposed;
    private bool isSuspended;
    private bool isVisible;
    private float width;
    private float height;

    private ThumbnailCompositionPreview(FrameworkElement host,
        IWindowPreview preview,
        SystemVisualProxyVisualPrivate proxy,
        ILogger logger)
    {
        this.host = host;
        this.preview = preview;
        this.proxy = proxy;
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

        try
        {
            Visual hostVisual = ElementCompositionPreview.GetElementVisual(host);
            proxy = SystemVisualProxyVisualPrivate.Create(hostVisual.Compositor);
            ElementCompositionPreview.SetElementChildVisual(host, proxy.Visual);

            return new ThumbnailCompositionPreview(host, preview, proxy, logger);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to create the composition thumbnail preview");
            TryDetach(host, proxy?.Visual, logger);
            proxy?.Dispose();
            preview.Dispose();
            return null;
        }
    }

    public void SetSuspended(bool isSuspended)
    {
        if (isDisposed || this.isSuspended == isSuspended)
        {
            return;
        }

        this.isSuspended = isSuspended;
        ApplyTarget();
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
        ApplyTarget();
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        preview.SetTarget(0, 0.0, 0.0, false);
        TryDetach(host, proxy.Visual, logger);
        proxy.Dispose();
        preview.Dispose();
        GC.SuppressFinalize(this);
    }

    private void ApplyTarget()
    {
        bool effectiveVisibility = isVisible && !isSuspended;
        proxy.Visual.IsVisible = effectiveVisibility;
        preview.SetTarget(proxy.Handle, width, height, isVisible);
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

    private static float NormalizeLength(double value)
    {
        if (!double.IsFinite(value) || value <= 0.0)
        {
            return 0.0f;
        }

        return (float)Math.Min(value, float.MaxValue);
    }
}

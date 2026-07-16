using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Numerics;

namespace Infinity.Shell.WinUI;

public class ThumbnailCompositionTarget :
    IDisposable
{
    private readonly FrameworkElement host;
    private readonly IWindowPreviewSurface previewSurface;
    private readonly SystemVisualProxyVisualPrivate proxy;
    private readonly ILogger logger;
    private bool isDisposed;

    private ThumbnailCompositionTarget(FrameworkElement host,
        IWindowPreviewSurface previewSurface,
        SystemVisualProxyVisualPrivate proxy,
        ILogger logger)
    {
        this.host = host;
        this.previewSurface = previewSurface;
        this.proxy = proxy;
        this.logger = logger;
        host.SizeChanged += HandleHostSizeChanged;
    }

    public static ThumbnailCompositionTarget? Create(FrameworkElement host,
        IWindowPreviewSurface previewSurface,
        ILogger logger)
    {
        if (!previewSurface.IsAvailable)
        {
            return null;
        }

        SystemVisualProxyVisualPrivate? proxy = null;

        try
        {
            Visual hostVisual = ElementCompositionPreview.GetElementVisual(host);
            proxy = SystemVisualProxyVisualPrivate.Create(hostVisual.Compositor);
            SetVisualSize(proxy.Visual, host.ActualWidth, host.ActualHeight);
            ElementCompositionPreview.SetElementChildVisual(host, proxy.Visual);
            previewSurface.SetTarget(proxy.Handle);
            return new ThumbnailCompositionTarget(host, previewSurface, proxy, logger);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to create the shared DWM thumbnail composition target");
            TryDetach(host, proxy?.Visual, logger);
            proxy?.Dispose();
            previewSurface.SetTarget(0);
            return null;
        }
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        host.SizeChanged -= HandleHostSizeChanged;
        previewSurface.SetTarget(0);
        TryDetach(host, proxy.Visual, logger);
        proxy.Dispose();
        GC.SuppressFinalize(this);
    }

    private void HandleHostSizeChanged(object sender, SizeChangedEventArgs args)
    {
        try
        {
            SetVisualSize(proxy.Visual, args.NewSize.Width, args.NewSize.Height);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to resize the shared DWM thumbnail composition target");
        }
    }

    private static void SetVisualSize(Visual visual, double width, double height)
    {
        float normalizedWidth = double.IsFinite(width) && width > 0.0
            ? (float)Math.Min(width, float.MaxValue)
            : 0.0f;
        float normalizedHeight = double.IsFinite(height) && height > 0.0
            ? (float)Math.Min(height, float.MaxValue)
            : 0.0f;
        visual.Size = new Vector2(normalizedWidth, normalizedHeight);
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
            logger.LogWarning(exception, "Failed to detach the shared DWM thumbnail composition target");
        }
    }
}

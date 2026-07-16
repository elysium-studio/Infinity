using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using System;
using Windows.Foundation;

namespace Infinity.Shell.WinUI;

public class ThumbnailCompositionPreview :
    IDisposable
{
    private readonly FrameworkElement host;
    private readonly FrameworkElement compositionHost;
    private readonly IWindowPreview preview;
    private readonly ILogger logger;
    private bool hasLoggedPlacementFailure;
    private bool isDisposed;

    private ThumbnailCompositionPreview(FrameworkElement host,
        FrameworkElement compositionHost,
        IWindowPreview preview,
        ILogger logger)
    {
        this.host = host;
        this.compositionHost = compositionHost;
        this.preview = preview;
        this.logger = logger;
    }

    public static ThumbnailCompositionPreview? Create(IWindowPreviewSurface previewSurface,
        nint windowHandle,
        FrameworkElement host,
        FrameworkElement compositionHost,
        ILogger logger)
    {
        if (!previewSurface.IsAvailable)
        {
            return null;
        }

        IWindowPreview? preview = previewSurface.CreatePreview(windowHandle);

        return preview is null
            ? null
            : new ThumbnailCompositionPreview(host, compositionHost, preview, logger);
    }

    public void Update(int zIndex, bool isVisible, bool isElevated)
    {
        if (isDisposed)
        {
            return;
        }

        double width = host.ActualWidth;
        double height = host.ActualHeight;

        if (!isVisible || width <= 0.0 || height <= 0.0 ||
            !double.IsFinite(width) || !double.IsFinite(height))
        {
            preview.SetPlacement(0.0, 0.0, 0.0, 0.0, zIndex, false, isElevated);
            return;
        }

        try
        {
            Rect bounds = host.TransformToVisual(compositionHost).TransformBounds(new Rect(0.0, 0.0, width, height));

            if (!double.IsFinite(bounds.X) || !double.IsFinite(bounds.Y) ||
                !double.IsFinite(bounds.Width) || !double.IsFinite(bounds.Height) ||
                bounds.Width <= 0.0 || bounds.Height <= 0.0)
            {
                preview.SetPlacement(0.0, 0.0, 0.0, 0.0, zIndex, false, isElevated);
                return;
            }

            preview.SetPlacement(bounds.X,
                bounds.Y,
                bounds.Width,
                bounds.Height,
                zIndex,
                true,
                isElevated);
            hasLoggedPlacementFailure = false;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            if (!hasLoggedPlacementFailure)
            {
                hasLoggedPlacementFailure = true;
                logger.LogWarning(exception, "Failed to place the DWM thumbnail in the shared composition layer");
            }

            preview.SetPlacement(0.0, 0.0, 0.0, 0.0, zIndex, false, isElevated);
        }
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        preview.SetPlacement(0.0, 0.0, 0.0, 0.0, 0, false, false);
        preview.Dispose();
        GC.SuppressFinalize(this);
    }
}

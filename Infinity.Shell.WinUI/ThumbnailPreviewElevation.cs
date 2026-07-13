using Infinity.Platform.Abstractions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.Foundation;

namespace Infinity.Shell.WinUI;

internal class ThumbnailPreviewElevation :
    IDisposable
{
    private readonly Canvas overlay;
    private readonly FrameworkElement sourceHost;
    private readonly Border overlayHost;
    private readonly IWindowPreviewOverlay previewOverlay;
    private readonly ThumbnailProxyHandle proxyHandle;
    private double previewWidth;
    private double previewHeight;
    private bool isDisposed;

    private ThumbnailPreviewElevation(Canvas overlay,
        FrameworkElement sourceHost,
        Border overlayHost,
        IWindowPreviewOverlay previewOverlay,
        ThumbnailProxyHandle proxyHandle)
    {
        this.overlay = overlay;
        this.sourceHost = sourceHost;
        this.overlayHost = overlayHost;
        this.previewOverlay = previewOverlay;
        this.proxyHandle = proxyHandle;

        CompositionTarget.Rendering += HandleRendering;
    }

    public static ThumbnailPreviewElevation? TryCreate(Canvas overlay,
        FrameworkElement sourceHost,
        TrackedWindowViewModel viewModel)
    {
        if (viewModel.Preview is not IWindowPreviewOverlay previewOverlay ||
            viewModel.Preview.KeepAlive is not ThumbnailProxyHandle ||
            !TryGetBounds(sourceHost, overlay, out Rect bounds))
        {
            return null;
        }

        Border overlayHost = new()
        {
            Width = bounds.Width,
            Height = bounds.Height,
            IsHitTestVisible = false
        };

        Canvas.SetLeft(overlayHost, bounds.X);
        Canvas.SetTop(overlayHost, bounds.Y);
        overlay.Children.Add(overlayHost);

        if (!ThumbnailProxyManager.TryAttachTemporary(overlayHost,
            bounds.Width,
            bounds.Height,
            out ThumbnailProxyHandle? proxyHandle) || proxyHandle is null)
        {
            overlay.Children.Remove(overlayHost);
            return null;
        }

        ThumbnailPreviewElevation elevation = new(overlay,
            sourceHost,
            overlayHost,
            previewOverlay,
            proxyHandle);

        try
        {
            elevation.SetPreviewSize(bounds.Width, bounds.Height);
            return elevation;
        }
        catch
        {
            elevation.Dispose();
            return null;
        }
    }

    public void Update()
    {
        if (isDisposed || !TryGetBounds(sourceHost, overlay, out Rect bounds))
        {
            return;
        }

        Canvas.SetLeft(overlayHost, bounds.X);
        Canvas.SetTop(overlayHost, bounds.Y);
        overlayHost.Width = bounds.Width;
        overlayHost.Height = bounds.Height;

        if (Math.Abs(previewWidth - bounds.Width) >= 0.5 ||
            Math.Abs(previewHeight - bounds.Height) >= 0.5)
        {
            SetPreviewSize(bounds.Width, bounds.Height);
        }
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        CompositionTarget.Rendering -= HandleRendering;

        try
        {
            previewOverlay.ClearOverlayTarget();
        }
        finally
        {
            ThumbnailProxyManager.ReleaseTemporary(overlayHost, proxyHandle);
            overlay.Children.Remove(overlayHost);
        }

        GC.SuppressFinalize(this);
    }

    private void HandleRendering(object? sender, object args) => Update();

    private void SetPreviewSize(double width, double height)
    {
        if (!ThumbnailProxyManager.UpdateSize(proxyHandle, width, height))
        {
            return;
        }

        previewWidth = width;
        previewHeight = height;
        previewOverlay.SetOverlayTarget(proxyHandle.Proxy.Handle, width, height, true);
    }

    private static bool TryGetBounds(FrameworkElement sourceHost,
        Canvas overlay,
        out Rect bounds)
    {
        bounds = default;

        if (sourceHost.ActualWidth <= 0.0 || sourceHost.ActualHeight <= 0.0)
        {
            return false;
        }

        try
        {
            bounds = sourceHost.TransformToVisual(overlay).TransformBounds(
                new Rect(0.0, 0.0, sourceHost.ActualWidth, sourceHost.ActualHeight));

            return double.IsFinite(bounds.X) &&
                double.IsFinite(bounds.Y) &&
                double.IsFinite(bounds.Width) &&
                double.IsFinite(bounds.Height) &&
                bounds.Width > 0.0 &&
                bounds.Height > 0.0;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}

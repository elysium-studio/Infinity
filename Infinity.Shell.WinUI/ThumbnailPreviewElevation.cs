using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
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
    private readonly ThumbnailProxyHandle proxyHandle;
    private bool isElevated;
    private bool isDisposed;

    private ThumbnailPreviewElevation(Canvas overlay,
        FrameworkElement sourceHost,
        Border overlayHost,
        ThumbnailProxyHandle proxyHandle)
    {
        this.overlay = overlay;
        this.sourceHost = sourceHost;
        this.overlayHost = overlayHost;
        this.proxyHandle = proxyHandle;
    }

    public static ThumbnailPreviewElevation? TryCreate(Canvas overlay,
        FrameworkElement sourceHost,
        TrackedWindowViewModel viewModel)
    {
        if (viewModel.Preview?.KeepAlive is not ThumbnailProxyHandle proxyHandle ||
            !TryGetBounds(sourceHost, overlay, out Rect bounds))
        {
            return null;
        }

        Border overlayHost = new()
        {
            Width = bounds.Width,
            Height = bounds.Height,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };

        Canvas.SetLeft(overlayHost, bounds.X);
        Canvas.SetTop(overlayHost, bounds.Y);
        overlay.Children.Add(overlayHost);

        ThumbnailPreviewElevation elevation = new(overlay,
            sourceHost,
            overlayHost,
            proxyHandle);

        if (elevation.TryElevate())
        {
            return elevation;
        }

        elevation.Dispose();
        return null;
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
            if (isElevated)
            {
                TrySetChildVisual(overlayHost, null);
                TrySetChildVisual(sourceHost, proxyHandle.Visual);
                isElevated = false;
            }
        }
        finally
        {
            overlay.Children.Remove(overlayHost);
        }

        GC.SuppressFinalize(this);
    }

    private void HandleRendering(object? sender, object args) => Update();

    private bool TryElevate()
    {
        if (isDisposed)
        {
            return false;
        }

        Update();
        overlayHost.Visibility = Visibility.Visible;

        if (!TrySetChildVisual(sourceHost, null) ||
            !TrySetChildVisual(overlayHost, proxyHandle.Visual))
        {
            TrySetChildVisual(overlayHost, null);
            TrySetChildVisual(sourceHost, proxyHandle.Visual);
            overlayHost.Visibility = Visibility.Collapsed;
            return false;
        }

        isElevated = true;
        CompositionTarget.Rendering += HandleRendering;
        return true;
    }

    private static bool TrySetChildVisual(FrameworkElement host, Visual? visual)
    {
        try
        {
            ElementCompositionPreview.SetElementChildVisual(host, visual);
            return true;
        }
        catch
        {
            return false;
        }
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

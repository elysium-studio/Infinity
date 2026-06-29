using Infinity.Platform.Abstractions;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Numerics;

namespace Infinity.Shell.WinUI;

public static class ThumbnailProxyManager
{
    public static bool TryAttach(IWindowPreview preview, FrameworkElement host, out nint proxyHandle)
    {
        proxyHandle = 0;

        try
        {
            Visual elementVisual = ElementCompositionPreview.GetElementVisual(host);
            Compositor compositor = elementVisual.Compositor;

            if (preview.KeepAlive is ThumbnailProxyHandle existingHandle)
            {
                if (ReferenceEquals(existingHandle.Visual.Compositor, compositor))
                {
                    ElementCompositionPreview.SetElementChildVisual(host, existingHandle.Visual);
                    proxyHandle = existingHandle.Proxy.Handle;
                    return true;
                }

                existingHandle.Dispose();
                preview.KeepAlive = null;
            }

            SystemVisualProxyVisualPrivate proxy = SystemVisualProxyVisualPrivate.Create(compositor);
            Visual proxyVisual = proxy.Visual;
            proxyVisual.Offset = new Vector3(0.0f, 0.0f, 0.0f);
            proxyVisual.Clip = compositor.CreateInsetClip();

            ThumbnailProxyHandle newHandle = new(proxy, proxyVisual);

            preview.KeepAlive = newHandle;

            ElementCompositionPreview.SetElementChildVisual(host, proxyVisual);

            proxyHandle = proxy.Handle;

            return true;
        }
        catch (Exception exception)
        {
            return false;
        }
    }

    public static void UpdateSize(IWindowPreview preview, double width, double height)
    {
        if (preview.KeepAlive is not ThumbnailProxyHandle handle)
        {
            return;
        }

        handle.Visual.Offset = new Vector3(0.0f, 0.0f, 0.0f);
        handle.Visual.Size = new Vector2((float)Math.Max(0.0, width), (float)Math.Max(0.0, height));
        handle.Visual.Scale = new Vector3(1.0f, 1.0f, 1.0f);
    }
}
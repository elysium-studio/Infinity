using Infinity.Platform.Abstractions;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Numerics;
using System.Runtime.InteropServices;

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
                if (TryAttachExisting(existingHandle, host, compositor, out proxyHandle))
                {
                    return true;
                }

                ClearExisting(preview, host, existingHandle);
            }

            return TryCreateAndAttach(preview, host, compositor, out proxyHandle);
        }
        catch
        {
            proxyHandle = 0;
            return false;
        }
    }

    public static bool UpdateSize(IWindowPreview preview, double width, double height)
    {
        if (preview.KeepAlive is not ThumbnailProxyHandle handle)
        {
            return false;
        }

        try
        {
            handle.Visual.Offset = new Vector3(0.0f, 0.0f, 0.0f);
            handle.Visual.Size = new Vector2(NormalizeLength(width), NormalizeLength(height));
            handle.Visual.Scale = new Vector3(1.0f, 1.0f, 1.0f);

            return true;
        }
        catch (ObjectDisposedException)
        {
            ClearExisting(preview, null, handle);
            return false;
        }
        catch (COMException)
        {
            ClearExisting(preview, null, handle);
            return false;
        }
        catch (InvalidOperationException)
        {
            ClearExisting(preview, null, handle);
            return false;
        }
    }

    private static bool TryAttachExisting(
        ThumbnailProxyHandle existingHandle,
        FrameworkElement host,
        Compositor compositor,
        out nint proxyHandle)
    {
        proxyHandle = 0;

        try
        {
            Compositor existingCompositor = existingHandle.Visual.Compositor;

            if (!ReferenceEquals(existingCompositor, compositor))
            {
                return false;
            }

            nint existingProxyHandle = existingHandle.Proxy.Handle;

            if (existingProxyHandle == 0)
            {
                return false;
            }

            ElementCompositionPreview.SetElementChildVisual(host, existingHandle.Visual);

            proxyHandle = existingProxyHandle;

            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
        catch (COMException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool TryCreateAndAttach(
        IWindowPreview preview,
        FrameworkElement host,
        Compositor compositor,
        out nint proxyHandle)
    {
        proxyHandle = 0;
        ThumbnailProxyHandle? handle = null;

        try
        {
            SystemVisualProxyVisualPrivate proxy = SystemVisualProxyVisualPrivate.Create(compositor);
            Visual visual = proxy.Visual;

            visual.Offset = new Vector3(0.0f, 0.0f, 0.0f);
            visual.Size = new Vector2(0.0f, 0.0f);
            visual.Scale = new Vector3(1.0f, 1.0f, 1.0f);
            visual.Clip = compositor.CreateInsetClip();

            handle = new ThumbnailProxyHandle(proxy, visual);

            ElementCompositionPreview.SetElementChildVisual(host, visual);

            proxyHandle = proxy.Handle;

            if (proxyHandle == 0)
            {
                SafeDispose(handle);
                return false;
            }

            preview.KeepAlive = handle;

            return true;
        }
        catch
        {
            SafeDispose(handle);
            proxyHandle = 0;
            return false;
        }
    }

    private static void ClearExisting(IWindowPreview preview, FrameworkElement? host, ThumbnailProxyHandle handle)
    {
        if (ReferenceEquals(preview.KeepAlive, handle))
        {
            preview.KeepAlive = null;
        }

        if (host is not null)
        {
            TryClearChildVisual(host);
        }

        SafeDispose(handle);
    }

    private static void TryClearChildVisual(FrameworkElement host)
    {
        try
        {
            ElementCompositionPreview.SetElementChildVisual(host, null);
        }
        catch
        {
        }
    }

    private static void SafeDispose(ThumbnailProxyHandle? handle)
    {
        if (handle is null)
        {
            return;
        }

        try
        {
            handle.Dispose();
        }
        catch
        {
        }
    }

    private static float NormalizeLength(double value)
    {
        if (double.IsNaN(value))
        {
            return 0.0f;
        }

        if (double.IsInfinity(value))
        {
            return 0.0f;
        }

        if (value < 0.0)
        {
            return 0.0f;
        }

        return (float)value;
    }
}
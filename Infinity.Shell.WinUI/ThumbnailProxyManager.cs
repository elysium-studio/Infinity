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
    private const float DefaultThumbnailCornerRadius = 8.0f;

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
            UpdateSizeCore(handle, width, height);

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

    internal static bool TryAttachTemporary(FrameworkElement host,
        double width,
        double height,
        out ThumbnailProxyHandle? handle)
    {
        handle = null;

        try
        {
            Compositor compositor = ElementCompositionPreview.GetElementVisual(host).Compositor;

            if (!TryCreateAndAttach(host, compositor, out handle) || handle is null)
            {
                return false;
            }

            if (UpdateSize(handle, width, height))
            {
                return true;
            }

            ReleaseTemporary(host, handle);
            handle = null;
            return false;
        }
        catch
        {
            ReleaseTemporary(host, handle);
            handle = null;
            return false;
        }
    }

    internal static bool UpdateSize(ThumbnailProxyHandle handle, double width, double height)
    {
        try
        {
            UpdateSizeCore(handle, width, height);
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

    internal static void ReleaseTemporary(FrameworkElement host, ThumbnailProxyHandle? handle)
    {
        TryClearChildVisual(host);
        SafeDispose(handle);
    }

    private static bool TryAttachExisting(ThumbnailProxyHandle existingHandle, FrameworkElement host, Compositor compositor, out nint proxyHandle)
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

    private static bool TryCreateAndAttach(IWindowPreview preview, FrameworkElement host, Compositor compositor, out nint proxyHandle)
    {
        proxyHandle = 0;

        if (!TryCreateAndAttach(host, compositor, out ThumbnailProxyHandle? handle) || handle is null)
        {
            return false;
        }

        proxyHandle = handle.Proxy.Handle;
        preview.KeepAlive = handle;

        return true;
    }

    private static bool TryCreateAndAttach(FrameworkElement host,
        Compositor compositor,
        out ThumbnailProxyHandle? handle)
    {
        handle = null;

        try
        {
            SystemVisualProxyVisualPrivate proxy = SystemVisualProxyVisualPrivate.Create(compositor);
            Visual visual = proxy.Visual;

            visual.Offset = new Vector3(0.0f, 0.0f, 0.0f);
            visual.Size = new Vector2(0.0f, 0.0f);
            visual.Scale = new Vector3(1.0f, 1.0f, 1.0f);

            ApplyClip(visual, 0.0f, 0.0f, DefaultThumbnailCornerRadius);

            handle = new ThumbnailProxyHandle(proxy, visual);

            ElementCompositionPreview.SetElementChildVisual(host, visual);

            if (proxy.Handle == 0)
            {
                SafeDispose(handle);
                handle = null;
                return false;
            }

            return true;
        }
        catch
        {
            SafeDispose(handle);
            handle = null;
            return false;
        }
    }

    private static void UpdateSizeCore(ThumbnailProxyHandle handle, double width, double height)
    {
        float normalizedWidth = NormalizeLength(width);
        float normalizedHeight = NormalizeLength(height);

        handle.Visual.Offset = new Vector3(0.0f, 0.0f, 0.0f);
        handle.Visual.Size = new Vector2(normalizedWidth, normalizedHeight);
        handle.Visual.Scale = new Vector3(1.0f, 1.0f, 1.0f);

        ApplyClip(handle.Visual, normalizedWidth, normalizedHeight, DefaultThumbnailCornerRadius);
    }

    private static void ApplyClip(Visual visual, float width, float height, float cornerRadius)
    {
        try
        {
            Compositor compositor = visual.Compositor;

            if (width <= 0.0f || height <= 0.0f)
            {
                visual.Clip = compositor.CreateInsetClip();
                return;
            }

            if (cornerRadius <= 0.0f)
            {
                visual.Clip = compositor.CreateInsetClip();
                return;
            }

            float normalizedCornerRadius = MathF.Min(
                cornerRadius,
                MathF.Min(width, height) / 2.0f);

            CompositionRoundedRectangleGeometry geometry = compositor.CreateRoundedRectangleGeometry();
            geometry.Offset = new Vector2(0.0f, 0.0f);
            geometry.Size = new Vector2(width, height);
            geometry.CornerRadius = new Vector2(normalizedCornerRadius, normalizedCornerRadius);

            CompositionGeometricClip clip = compositor.CreateGeometricClip(geometry);
            visual.Clip = clip;
        }
        catch
        {
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

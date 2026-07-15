using Infinity.Platform.Abstractions;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Infinity.Shell.WinUI;

public static class ThumbnailProxyManager
{
    private const float DefaultThumbnailCornerRadius = 8.0f;
    private const int HandoverFallbackFrameLimit = 120;

    private static readonly Dictionary<IWindowPreview, PendingHandover> pendingHandovers = new();
    private static bool isRenderingHooked;

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

    public static bool TryReattach(IWindowPreview preview, FrameworkElement host, out nint proxyHandle)
    {
        proxyHandle = 0;

        try
        {
            CompleteHandover(preview);

            Visual elementVisual = ElementCompositionPreview.GetElementVisual(host);
            Compositor compositor = elementVisual.Compositor;
            ThumbnailProxyHandle? previousHandle = preview.KeepAlive as ThumbnailProxyHandle;

            if (previousHandle is null)
            {
                return TryCreateAndAttach(preview, host, compositor, out proxyHandle);
            }

            if (ElementCompositionPreview.GetElementChildVisual(host) is not ContainerVisual container ||
                !container.Children.Contains(previousHandle.Visual))
            {
                ClearExisting(preview, host, previousHandle);
                return TryCreateAndAttach(preview, host, compositor, out proxyHandle);
            }

            if (!TryCreateProxy(compositor, out ThumbnailProxyHandle? newHandle) || newHandle is null)
            {
                return false;
            }

            try
            {
                container.Children.InsertAtTop(newHandle.Visual);
            }
            catch
            {
                SafeDispose(newHandle);
                return false;
            }

            proxyHandle = newHandle.Proxy.Handle;
            preview.KeepAlive = newHandle;
            RegisterHandover(preview, previousHandle);

            return true;
        }
        catch
        {
            proxyHandle = 0;
            return false;
        }
    }

    public static void CompleteHandover(IWindowPreview preview)
    {
        if (!pendingHandovers.TryGetValue(preview, out PendingHandover? pending))
        {
            return;
        }

        pendingHandovers.Remove(preview);
        RemoveFromParent(pending.OldHandle.Visual);
        SafeDispose(pending.OldHandle);
        UnhookRenderingIfIdle();
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

    private static void RegisterHandover(IWindowPreview preview, ThumbnailProxyHandle oldHandle)
    {
        pendingHandovers[preview] = new PendingHandover(oldHandle, HandoverFallbackFrameLimit);

        if (!isRenderingHooked)
        {
            isRenderingHooked = true;
            CompositionTarget.Rendering += HandleRendering;
        }
    }

    private static void HandleRendering(object? sender, object args)
    {
        List<IWindowPreview>? expired = null;

        foreach (KeyValuePair<IWindowPreview, PendingHandover> entry in pendingHandovers)
        {
            entry.Value.RemainingFrames--;

            if (entry.Value.RemainingFrames <= 0)
            {
                expired ??= new List<IWindowPreview>();
                expired.Add(entry.Key);
            }
        }

        if (expired is not null)
        {
            foreach (IWindowPreview preview in expired)
            {
                CompleteHandover(preview);
            }
        }

        UnhookRenderingIfIdle();
    }

    private static void UnhookRenderingIfIdle()
    {
        if (isRenderingHooked && pendingHandovers.Count == 0)
        {
            CompositionTarget.Rendering -= HandleRendering;
            isRenderingHooked = false;
        }
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

            if (ElementCompositionPreview.GetElementChildVisual(host) is not ContainerVisual container ||
                !container.Children.Contains(existingHandle.Visual))
            {
                return false;
            }

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

        if (!TryCreateProxy(compositor, out ThumbnailProxyHandle? handle) || handle is null)
        {
            return false;
        }

        try
        {
            ContainerVisual container = compositor.CreateContainerVisual();
            container.RelativeSizeAdjustment = Vector2.One;
            container.Children.InsertAtTop(handle.Visual);
            ElementCompositionPreview.SetElementChildVisual(host, container);
        }
        catch
        {
            SafeDispose(handle);
            return false;
        }

        proxyHandle = handle.Proxy.Handle;
        preview.KeepAlive = handle;

        return true;
    }

    private static bool TryCreateProxy(Compositor compositor, out ThumbnailProxyHandle? handle)
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

        RemoveFromParent(handle.Visual);
        SafeDispose(handle);
    }

    private static void RemoveFromParent(Visual visual)
    {
        try
        {
            if (visual.Parent is ContainerVisual parent)
            {
                parent.Children.Remove(visual);
            }
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

    private sealed class PendingHandover
    {
        public PendingHandover(ThumbnailProxyHandle oldHandle, int remainingFrames)
        {
            OldHandle = oldHandle;
            RemainingFrames = remainingFrames;
        }

        public ThumbnailProxyHandle OldHandle { get; }

        public int RemainingFrames { get; set; }
    }
}
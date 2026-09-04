using Infinity.Platform.Abstractions;
using Infinity.Platform.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Infinity.Shell.WinUI;

// Overlay lifetime only. Per-window capture and visual presentation live in
// separate objects; the application/session layer has no WinUI dependency.
public sealed class WindowCapturePreviewSurface(WindowCaptureSupport support, WindowCaptureAccess access, ILogger<WindowCapturePreviewSurface> logger) : IWindowPreviewSurface, IDisposable
{
    private readonly HashSet<WindowCapturePreview> previews = [];
    private readonly object gate = new();
    private bool active;
    private bool borderless;
    private bool accessResolved;
    private bool disposed;
    private Task? accessRequest;

    public bool IsAvailable => !disposed && support.IsSupported;

    public WindowCapturePreview? CreatePreview(nint windowHandle)
    {
        if (!IsAvailable || windowHandle == 0) return null;
        try
        {
            WindowCapturePreview preview = new(windowHandle, logger, Remove);
            lock (gate) previews.Add(preview);
            preview.SetBorderless(borderless);
            preview.SetActive(active && accessResolved);
            return preview;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Cannot capture HWND {WindowHandle}; leaving a contained placeholder", windowHandle);
            return null;
        }
    }

    public void Initialize(nint ownerWindowHandle)
    {
        if (disposed) return;
        active = true;
        accessRequest ??= RequestAccessAsync(DispatcherQueue.GetForCurrentThread());
        if (accessResolved)
            foreach (WindowCapturePreview preview in Snapshot()) preview.SetActive(true);
    }

    private async Task RequestAccessAsync(DispatcherQueue dispatcher)
    {
        bool allowed = await access.RequestBorderlessAsync().ConfigureAwait(false);
        dispatcher.TryEnqueue(() =>
        {
            if (disposed) return;
            borderless = allowed;
            accessResolved = true;
            foreach (WindowCapturePreview preview in Snapshot())
            {
                // Both commands use the same per-window queue: configure the
                // indicator before StartCapture, never after its first frame.
                preview.SetBorderless(borderless);
                preview.SetActive(active);
            }
        });
    }

    public void Clear()
    {
        active = false;
        foreach (WindowCapturePreview preview in Snapshot()) preview.SetActive(false);
    }

    private void Remove(WindowCapturePreview preview)
    {
        lock (gate) previews.Remove(preview);
    }

    private WindowCapturePreview[] Snapshot()
    {
        lock (gate)
        {
            WindowCapturePreview[] result = new WindowCapturePreview[previews.Count];
            previews.CopyTo(result);
            return result;
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (WindowCapturePreview preview in Snapshot()) preview.Dispose();
        lock (gate) previews.Clear();
        GC.SuppressFinalize(this);
    }
}

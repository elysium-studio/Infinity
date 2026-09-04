using Infinity.Platform.Windows;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Composition;
using System;
using Windows.Foundation;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.UI;

namespace Infinity.Shell.WinUI;

internal sealed class WindowCaptureFrameRenderer : IDisposable
{
    // All previews use Win2D's shared device/resource manager. Per-window locks
    // are insufficient: bitmap wrapping takes ResourceManager -> Direct2D,
    // while drawing-session creation takes Direct2D -> ResourceManager. Keep
    // these complete operations mutually exclusive across ALL thumbnails.
    private static readonly object GraphicsGate = new();
    private readonly CanvasSwapChain swapChain;
    private int width = 1;
    private int height = 1;

    public WindowCaptureFrameRenderer()
    {
        lock (GraphicsGate)
        {
            Device = CanvasDevice.GetSharedDevice();
            swapChain = new CanvasSwapChain(Device, 1, 1, 96, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, CanvasAlphaMode.Ignore);
            using (swapChain.CreateDrawingSession(Color.FromArgb(255, 32, 32, 32))) { }
            swapChain.Present(0);
        }
    }

    public CanvasDevice Device { get; }

    public ICompositionSurface CreateSurface(Compositor compositor)
    {
        lock (GraphicsGate) return WindowCaptureSwapChainInterop.CreateSurface(compositor, swapChain);
    }

    // Runs on the capture worker, never the XAML/animation thread. Win2D wraps
    // the D3D11 surface and draws GPU-to-GPU; no pixel readback or BitmapSource.
    public void Present(Direct3D11CaptureFrame frame, WindowCaptureFrameGeometry geometry)
    {
        lock (GraphicsGate)
        {
            using (CanvasBitmap bitmap = CanvasBitmap.CreateFromDirect3D11Surface(Device, frame.Surface))
            {
                if (width != geometry.Width || height != geometry.Height)
                {
                    swapChain.ResizeBuffers(geometry.Width, geometry.Height, 96);
                    width = geometry.Width;
                    height = geometry.Height;
                }

                Rect content = new(0, 0, width, height);
                using (CanvasDrawingSession drawing = swapChain.CreateDrawingSession(Color.FromArgb(255, 32, 32, 32)))
                {
                    // The remainder of a pool buffer after a shrink is undefined.
                    // Copy only ContentSize, not the entire frame-pool allocation.
                    drawing.DrawImage(bitmap, content, content);
                }
            }
            // Win2D's Present also takes its Direct2D resource lock. Keep it
            // in the same serialised operation, after disposing both wrappers.
            swapChain.Present(0);
        }
    }

    public void Dispose()
    {
        lock (GraphicsGate) swapChain.Dispose();
    }
}

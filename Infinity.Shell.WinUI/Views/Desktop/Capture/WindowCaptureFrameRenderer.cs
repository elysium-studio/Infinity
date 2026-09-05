using System;
using System.Threading;
using Infinity.Platform.Windows;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Composition;
using Windows.Foundation;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.UI;

namespace Infinity.Shell.WinUI;

internal sealed class WindowCaptureFrameRenderer : IDisposable
{
    private static readonly Lock GraphicsGate = new();
    private readonly CanvasSwapChain swapChain;
    private int width = 1;
    private int height = 1;

    public WindowCaptureFrameRenderer()
    {
        lock (GraphicsGate)
        {
            Device = CanvasDevice.GetSharedDevice();
            swapChain = new(Device, 1, 1, 96, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, CanvasAlphaMode.Ignore);
            using (swapChain.CreateDrawingSession(Color.FromArgb(255, 32, 32, 32)))
            {
            }

            swapChain.Present(0);
        }
    }


    public CanvasDevice Device { get; }


    public ICompositionSurface CreateSurface(Compositor compositor)
    {
        lock (GraphicsGate)
        {
            return WindowCaptureSwapChainInterop.CreateSurface(compositor, swapChain);
        }
    }


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
                    drawing.DrawImage(bitmap, content, content);
                }
            }

            swapChain.Present(0);
        }
    }


    public void Dispose()
    {
        lock (GraphicsGate)
        {
            swapChain.Dispose();
        }
    }
}

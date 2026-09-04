using Infinity.Platform.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using System;
using System.Threading;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;

namespace Infinity.Shell.WinUI;

public sealed class WindowCapturePreview : IDisposable
{
    private readonly object gate = new();
    private readonly GraphicsCaptureItem item;
    private readonly ILogger logger;
    private readonly Action<WindowCapturePreview> onDisposed;
    private readonly WindowCaptureWorkQueue work;
    private readonly DispatcherQueue dispatcher;
    private readonly WindowCaptureFrameState frameState = new();
    private long sessionGeneration;
    private int requestedVisibility;
    private int requestedActive;
    private int disposeRequested;
    private int framePending;
    private WindowCaptureFrameRenderer renderer;
    private Direct3D11CaptureFramePool? framePool;
    private GraphicsCaptureSession? session;
    private SizeInt32 poolSize;
    private bool active;
    private bool visible;
    private bool borderless;
    private bool closed;
    private bool disposed;
    private bool failed;
    private bool deviceLost;

    internal WindowCapturePreview(nint windowHandle, ILogger logger, Action<WindowCapturePreview> onDisposed)
    {
        WindowHandle = windowHandle;
        this.logger = logger;
        this.onDisposed = onDisposed;
        dispatcher = DispatcherQueue.GetForCurrentThread();
        work = new(exception => logger.LogError(exception, "Capture worker failed for HWND {WindowHandle}", windowHandle));
        item = WindowCaptureItemFactory.Create(windowHandle);
        renderer = new WindowCaptureFrameRenderer();
        item.Closed += HandleClosed;
    }

    public nint WindowHandle { get; }

    public event Action? SurfaceChanged;

    public event Action? FrameAvailabilityChanged;

    public bool HasCurrentFrame => frameState.HasCurrentFrame;

    private void InvalidateFrame()
    {
        frameState.Invalidate();
        NotifyFrameAvailabilityChanged();
    }

    private void NotifyFrameAvailabilityChanged()
    {
        void Notify()
        {
            if (Volatile.Read(ref disposeRequested) == 0) FrameAvailabilityChanged?.Invoke();
        }

        if (dispatcher.HasThreadAccess) Notify();
        else dispatcher.TryEnqueue(Notify);
    }

    public ICompositionSurface CreateSurface(Compositor compositor)
    {
        lock (gate) return renderer.CreateSurface(compositor);
    }

    public void SetVisible(bool value)
    {
        int requested = value ? 1 : 0;
        if (Volatile.Read(ref disposeRequested) != 0 || Interlocked.Exchange(ref requestedVisibility, requested) == requested) return;
        InvalidateFrame();
        work.Enqueue(() =>
        {
            if (Volatile.Read(ref requestedVisibility) == requested) SetVisibleCore(value);
        });
    }

    private void SetVisibleCore(bool value)
    {
        lock (gate)
        {
            if (disposed) return;
            visible = value;
        }
        UpdateSession();
    }

    internal void SetActive(bool value)
    {
        if (Volatile.Read(ref disposeRequested) != 0) return;
        int requested = value ? 1 : 0;
        Volatile.Write(ref requestedActive, requested);
        // Even a rapid reopen before queued shutdown completes starts a new
        // freshness epoch. The previous swap-chain contents are not displayable.
        InvalidateFrame();
        work.Enqueue(() =>
        {
            if (Volatile.Read(ref requestedActive) == requested) SetActiveCore(value);
        });
    }

    private void SetActiveCore(bool value)
    {
        bool retry;
        lock (gate) retry = value && failed;
        if (retry) StopSession();
        bool surfaceChanged = false;
        lock (gate)
        {
            if (disposed) return;
            active = value;
            if (value && failed)
            {
                failed = false;
                if (deviceLost)
                {
                    try
                    {
                        WindowCaptureFrameRenderer replacement = new();
                        renderer.Dispose();
                        renderer = replacement;
                        deviceLost = false;
                        surfaceChanged = true;
                    }
                    catch (Exception exception)
                    {
                        RecordFailure(exception);
                    }
                }
            }
        }
        if (surfaceChanged) dispatcher.TryEnqueue(() =>
        {
            if (Volatile.Read(ref disposeRequested) == 0) SurfaceChanged?.Invoke();
        });
        UpdateSession();
    }

    internal void SetBorderless(bool value)
    {
        if (Volatile.Read(ref disposeRequested) != 0) return;
        work.Enqueue(() => SetBorderlessCore(value));
    }

    private void SetBorderlessCore(bool value)
    {
        lock (gate)
        {
            borderless = value;
            if (session is not null)
            {
                try { WindowCaptureSessionOptions.Apply(session, borderless); }
                catch (Exception exception) { logger.LogWarning(exception, "Cannot update capture options for HWND {WindowHandle}", WindowHandle); }
            }
        }
    }

    private void UpdateSession()
    {
        long generation = frameState.Generation;
        if (session is not null && sessionGeneration != generation) StopSession();
        lock (gate)
        {
            if (!disposed && !closed && !failed && active && visible)
            {
                if (session is not null) return;
                try
                {
                    poolSize = item.Size;
                    if (poolSize.Width <= 0 || poolSize.Height <= 0) return;
                    framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(renderer.Device,
                        DirectXPixelFormat.B8G8R8A8UIntNormalized, WindowCaptureFrameReader.BufferCount, poolSize);
                    session = framePool.CreateCaptureSession(item);
                    sessionGeneration = generation;
                    WindowCaptureSessionOptions.Apply(session, borderless);
                    framePool.FrameArrived += HandleFrameArrived;
                    session.StartCapture();
                    return;
                }
                catch (Exception exception)
                {
                    RecordFailure(exception);
                }
            }
        }
        StopSession();
    }

    private void HandleFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        // Native callbacks only signal work. Never render or close/recreate a
        // session inside its callback, or wait for the lifecycle worker here.
        if (Volatile.Read(ref disposeRequested) != 0 || Interlocked.CompareExchange(ref framePending, 1, 0) != 0) return;
        if (!work.Enqueue(() =>
        {
            Interlocked.Exchange(ref framePending, 0);
            ProcessFrame(sender);
        })) Interlocked.Exchange(ref framePending, 0);
    }

    private void ProcessFrame(Direct3D11CaptureFramePool sender)
    {
        lock (gate)
        {
            if (disposed || closed || !active || !visible || !sender.Equals(framePool) ||
                !frameState.IsCurrent(sessionGeneration)) return;
            try
            {
                // Never fall back to a foreign visual or screen-region copy.
                SizeInt32? resized = null;
                using (Direct3D11CaptureFrame? frame = WindowCaptureFrameReader.TakeLatest(sender))
                {
                    if (frame is null || failed) return;
                    SizeInt32 content = frame.ContentSize;
                    var description = frame.Surface.Description;
                    WindowCaptureFrameGeometry geometry = WindowCaptureFrameGeometry.Calculate(
                        content.Width, content.Height, description.Width, description.Height, poolSize.Width, poolSize.Height);
                    if (geometry.CanPresent)
                    {
                        renderer.Present(frame, geometry);
                        // Recheck after GPU presentation: closing/reopening may
                        // have invalidated this epoch while the copy was running.
                        if (frameState.TryMarkPresented(sessionGeneration)) NotifyFrameAvailabilityChanged();
                    }
                    if (geometry.RequiresPoolResize) resized = content;
                }
                // Release the old frame before recreating its pool. A growth
                // frame clipped by the previous allocation is never presented.
                if (resized is SizeInt32 nextSize)
                {
                    sender.Recreate(renderer.Device, DirectXPixelFormat.B8G8R8A8UIntNormalized, WindowCaptureFrameReader.BufferCount, nextSize);
                    poolSize = nextSize;
                }
            }
            catch (Exception exception)
            {
                RecordFailure(exception);
            }
        }
    }

    private void RecordFailure(Exception exception)
    {
        if (!failed)
        {
            logger.LogWarning(exception, "Window capture failed for HWND {WindowHandle}; hiding the unavailable live frame", WindowHandle);
            InvalidateFrame();
        }
        failed = true;
        deviceLost |= exception.HResult is unchecked((int)0x887A0005) or unchecked((int)0x887A0007) or unchecked((int)0x8899000C);
    }

    private void HandleClosed(GraphicsCaptureItem sender, object args)
    {
        InvalidateFrame();
        work.Enqueue(() =>
        {
            lock (gate) closed = true;
            StopSession();
        });
    }

    private void StopSession()
    {
        Direct3D11CaptureFramePool? oldPool;
        GraphicsCaptureSession? oldSession;
        lock (gate)
        {
            oldPool = framePool;
            oldSession = session;
            framePool = null;
            session = null;
        }
        // Closing can wait for a callback. Never hold the callback's lock here.
        try
        {
            if (oldPool is not null) oldPool.FrameArrived -= HandleFrameArrived;
            oldSession?.Dispose();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Cannot close capture session for HWND {WindowHandle}", WindowHandle);
        }
        finally
        {
            try { oldPool?.Dispose(); }
            catch (Exception exception) { logger.LogWarning(exception, "Cannot close capture buffers for HWND {WindowHandle}", WindowHandle); }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposeRequested, 1) != 0) return;
        frameState.Invalidate();
        SurfaceChanged = null;
        FrameAvailabilityChanged = null;
        onDisposed(this);
        work.Complete(DisposeCore);
        GC.SuppressFinalize(this);
    }

    private void DisposeCore()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
        }
        try
        {
            item.Closed -= HandleClosed;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Cannot detach the closed capture item for HWND {WindowHandle}", WindowHandle);
        }
        StopSession();
        try
        {
            lock (gate) renderer.Dispose();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Cannot release the capture renderer for HWND {WindowHandle}", WindowHandle);
        }
        finally
        {
            SurfaceChanged = null;
        }
    }
}

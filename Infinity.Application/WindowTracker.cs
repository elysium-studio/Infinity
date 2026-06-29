using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace Infinity.Application;

public class WindowTracker(IWindowStore repository,
    IWindowGeometryReader reader,
    IWindowFilter filter,
    IWindowAncestorResolver ancestorResolver,
    IWindowRestoreGuard restoreGuard,
    IWindowMoveGuard moveGuard,
    IWindowDragGuard dragGuard,
    IWindowEventListener listener,
    IPanState state,
    IntPtr handle,
    ILogger<WindowTracker> logger) :
    IWindowTracker
{
    private readonly Dictionary<IntPtr, int> minimizedWindows = [];

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    public event Action<IntPtr>? WindowRestored;

    public void Start()
    {
        listener.WindowCreated += HandleWindowCreated;
        listener.WindowShown += HandleWindowShown;
        listener.WindowDestroyed += HandleWindowDestroyed;
        listener.MinimizeStarted += HandleMinimizeStarted;
        listener.MinimizeEnded += HandleMinimizeEnded;
        listener.DragEnded += HandleDragEnded;
        listener.WindowLocationChanged += HandleWindowLocationChanged;
        state.OffsetChanged += HandleOffsetChanged;
    }

    public void Stop()
    {
        listener.WindowCreated -= HandleWindowCreated;
        listener.WindowShown -= HandleWindowShown;
        listener.WindowDestroyed -= HandleWindowDestroyed;
        listener.MinimizeStarted -= HandleMinimizeStarted;
        listener.MinimizeEnded -= HandleMinimizeEnded;
        listener.DragEnded -= HandleDragEnded;
        listener.WindowLocationChanged -= HandleWindowLocationChanged;
        state.OffsetChanged -= HandleOffsetChanged;
    }

    public void TryRegister(IntPtr windowHandle)
    {
        if (repository.TryGet(windowHandle, out _))
        {
            return;
        }

        if (!reader.IsVisible(windowHandle))
        {
            return;
        }

        if (reader.IsMinimised(windowHandle))
        {
            return;
        }

        if (!filter.ShouldTrack(windowHandle, handle))
        {
            IntPtr ancestor = ancestorResolver.GetRootAncestor(windowHandle);

            if (ancestor == windowHandle || ancestor == IntPtr.Zero)
            {
                return;
            }

            TryRegister(ancestor);
            return;
        }

        if (!reader.TryReadGeometry(windowHandle, out int x, out int y, out int width, out int height))
        {
            return;
        }

        bool isRestore = minimizedWindows.TryGetValue(windowHandle, out int previousCanvasX);

        if (isRestore)
        {
            restoreGuard.MarkRestoring(windowHandle);
            minimizedWindows.Remove(windowHandle);
        }

        int canvasX = isRestore ? previousCanvasX : x + (int)Math.Round(state.Offset);

        repository.Add(new TrackedWindow
        {
            Handle = windowHandle,
            CanvasX = canvasX,
            CanvasY = y,
            Width = width,
            Height = height,
            LastPlacedX = x,
            LastPlacedY = y,
            ZIndex = GetZIndex(windowHandle)
        });

        logger.LogInformation(isRestore
            ? "Window restored: {Handle} at canvas X {CanvasX}"
            : "Window registered: {Handle} at canvas X {CanvasX}",
            windowHandle, canvasX);

        if (isRestore)
        {
            WindowRestored?.Invoke(windowHandle);
        }
    }

    private void HandleWindowCreated(IntPtr windowHandle) => TryRegister(windowHandle);

    private void HandleWindowShown(IntPtr windowHandle) => TryRegister(windowHandle);

    private void HandleWindowDestroyed(IntPtr windowHandle) => Unregister(windowHandle);

    private void HandleMinimizeStarted(IntPtr windowHandle) => Unregister(windowHandle);

    private void HandleMinimizeEnded(IntPtr windowHandle) => TryRegister(windowHandle);

    private void HandleDragEnded(IntPtr windowHandle) => HandleWindowMovedExternally(windowHandle);

    private void HandleWindowLocationChanged(IntPtr windowHandle)
    {
        if (moveGuard.IsSystemMove)
        {
            return;
        }

        HandleWindowMovedExternally(windowHandle);
    }

    private void HandleOffsetChanged()
    {
        IntPtr draggingWindow = dragGuard.DraggingWindow;

        if (draggingWindow == IntPtr.Zero)
        {
            return;
        }

        if (!repository.TryGet(draggingWindow, out TrackedWindow trackedWindow))
        {
            return;
        }

        trackedWindow.CanvasX = trackedWindow.LastPlacedX + (int)Math.Round(state.Offset);
    }

    private void HandleWindowMovedExternally(IntPtr windowHandle)
    {
        if (!repository.TryGet(windowHandle, out TrackedWindow trackedWindow))
        {
            return;
        }

        if (restoreGuard.IsRestoring(windowHandle))
        {
            return;
        }

        bool visible = reader.IsVisible(windowHandle);
        bool minimised = reader.IsMinimised(windowHandle);

        if (!visible || minimised)
        {
            minimizedWindows[windowHandle] = trackedWindow.CanvasX;
            Unregister(windowHandle);
            return;
        }

        if (!reader.TryReadGeometry(windowHandle, out int x, out int y, out int width, out int height))
        {
            return;
        }

        if (x == trackedWindow.LastPlacedX && y == trackedWindow.LastPlacedY)
        {
            return;
        }

        trackedWindow.CanvasX = x + (int)Math.Round(state.Offset);
        trackedWindow.CanvasY = y;
        trackedWindow.Width = width;
        trackedWindow.Height = height;
        trackedWindow.LastPlacedX = x;
        trackedWindow.LastPlacedY = y;
        trackedWindow.ZIndex = GetZIndex(windowHandle);
    }

    private void Unregister(IntPtr windowHandle)
    {
        logger.LogInformation("Window unregistered: {Handle}", windowHandle);
        repository.Remove(windowHandle);
    }

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    private int GetZIndex(IntPtr windowHandle)
    {
        int result = int.MaxValue;
        int index = 0;

        EnumWindows((hwnd, _) =>
        {
            if (hwnd == windowHandle)
            {
                result = index;
                return false;
            }

            index++;
            return true;
        }, IntPtr.Zero);

        return result;
    }
}
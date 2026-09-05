using Elysium.Platform.Abstractions;
using Infinity.Platform.Abstractions;

namespace Infinity.Platform.Windows;

public sealed class WindowDragGuard(IWindowEventListener listener) : IWindowDragGuard
{
    private readonly Lock syncRoot = new();
    private readonly HashSet<nint> draggingWindows = [];
    private bool isStarted;

    public event Action? HoldStarted;

    public bool IsDragging(nint windowHandle)
    {
        lock (syncRoot)
        {
            return draggingWindows.Contains(windowHandle);
        }
    }


    public bool IsAnyDragging
    {
        get
        {
            lock (syncRoot)
            {
                return draggingWindows.Count > 0;
            }
        }
    }


    public nint DraggingWindow
    {
        get
        {
            lock (syncRoot)
            {
                return draggingWindows.FirstOrDefault(0);
            }
        }
    }


    public void Start()
    {
        lock (syncRoot)
        {
            if (isStarted)
            {
                return;
            }

            isStarted = true;
        }

        listener.DragStarted += HandleDragStarted;
        listener.DragEnded += HandleDragEnded;
    }


    public void Stop()
    {
        lock (syncRoot)
        {
            if (!isStarted)
            {
                return;
            }

            isStarted = false;
            draggingWindows.Clear();
        }

        listener.DragStarted -= HandleDragStarted;
        listener.DragEnded -= HandleDragEnded;
    }


    private void HandleDragStarted(nint windowHandle)
    {
        bool started;
        lock (syncRoot)
        {
            started = isStarted && draggingWindows.Add(windowHandle);
        }

        if (started)
        {
            HoldStarted?.Invoke();
        }
    }


    private void HandleDragEnded(nint windowHandle)
    {
        lock (syncRoot)
        {
            if (isStarted)
            {
                draggingWindows.Remove(windowHandle);
            }
        }
    }
}

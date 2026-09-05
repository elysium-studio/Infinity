using Elysium.Platform.Abstractions;
using Infinity.Platform.Windows;

namespace Infinity.Tests;

public sealed class WindowDragGuardTests
{
    [Fact]
    public void TracksNativeDragWithoutChangingWindowArranging()
    {
        TestWindowEventListener listener = new();
        WindowDragGuard guard = new(listener);
        int holdStartedCount = 0;
        guard.HoldStarted += () => holdStartedCount++;
        guard.Start();
        listener.RaiseDragStarted(1);
        Assert.True(guard.IsAnyDragging);
        Assert.True(guard.IsDragging(1));
        Assert.Equal((nint)1, guard.DraggingWindow);
        Assert.Equal(1, holdStartedCount);
        listener.RaiseDragEnded(1);
        Assert.False(guard.IsAnyDragging);
        Assert.False(guard.IsDragging(1));
        Assert.Equal(0, guard.DraggingWindow);
    }


    [Fact]
    public void StopClearsTrackedDragsAndUnsubscribes()
    {
        TestWindowEventListener listener = new();
        WindowDragGuard guard = new(listener);
        guard.Start();
        listener.RaiseDragStarted(1);
        guard.Stop();
        listener.RaiseDragStarted(2);
        Assert.False(guard.IsAnyDragging);
        Assert.Equal(0, guard.DraggingWindow);
    }


    private sealed class TestWindowEventListener : IWindowEventListener
    {
        public event Action<nint>? DragStarted;

        public event Action<nint>? DragEnded;

        event Action<nint>? IWindowEventListener.WindowCreated
        {
            add
            {
            }

            remove
            {
            }
        }


        event Action<nint>? IWindowEventListener.WindowShown
        {
            add
            {
            }

            remove
            {
            }
        }


        event Action<nint>? IWindowEventListener.WindowDestroyed
        {
            add
            {
            }

            remove
            {
            }
        }


        event Action<nint>? IWindowEventListener.WindowTitleChanged
        {
            add
            {
            }

            remove
            {
            }
        }


        event Action<nint>? IWindowEventListener.WindowLocationChanged
        {
            add
            {
            }

            remove
            {
            }
        }


        event Action<nint>? IWindowEventListener.MinimizeStarted
        {
            add
            {
            }

            remove
            {
            }
        }


        event Action<nint>? IWindowEventListener.MinimizeEnded
        {
            add
            {
            }

            remove
            {
            }
        }


        event Action<nint>? IWindowEventListener.ForegroundChanged
        {
            add
            {
            }

            remove
            {
            }
        }


        event Action? IWindowEventListener.WindowStackChanged
        {
            add
            {
            }

            remove
            {
            }
        }


        public void RaiseDragStarted(nint handle) => DragStarted?.Invoke(handle);

        public void RaiseDragEnded(nint handle) => DragEnded?.Invoke(handle);

        public void Start()
        {
        }


        public void Stop()
        {
        }


        public void Dispose() => GC.SuppressFinalize(this);
    }
}

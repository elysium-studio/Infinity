using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Infinity.Platform.Abstractions;
using Infinity.Platform.Windows;
using Microsoft.Extensions.Logging.Abstractions;

namespace Infinity.Tests;

public sealed class ForegroundWindowTrackerTests
{
    [Fact]
    public async Task NormalForegroundChangeIsPublished()
    {
        TestWindowEventListener listener = new();
        ForegroundWindowTracker tracker = new(listener, new TestWindowFocusGuard(), new TestDispatcher(), NullLogger<ForegroundWindowTracker>.Instance, TimeSpan.Zero, _ => true);
        TaskCompletionSource<nint> published = new(TaskCreationOptions.RunContinuationsAsynchronously);
        tracker.ForegroundWindowChanged += (_, handle) => published.TrySetResult(handle);
        tracker.Start();
        try
        {
            listener.RaiseForegroundChanged(new nint(1));
            Assert.Equal(new nint(1), await published.Task.WaitAsync(TimeSpan.FromSeconds(1)));
        }
        finally
        {
            tracker.Stop();
        }
    }


    [Fact]
    public void ClosingForegroundWindowBeforeFallbackIsClassified()
    {
        ForegroundWindowTracker.ForegroundTransitionHistory history = new();
        nint originalWindow = new(1);
        nint temporaryWindow = new(2);
        history.RecordForeground(originalWindow);
        history.RecordForeground(temporaryWindow);
        history.RecordDestroyed(temporaryWindow);
        history.RecordForeground(originalWindow);
        Assert.True(history.TakePending().IsCloseFallback);
    }


    [Fact]
    public void ClosingForegroundWindowAfterFallbackIsClassified()
    {
        ForegroundWindowTracker.ForegroundTransitionHistory history = new();
        nint originalWindow = new(1);
        nint temporaryWindow = new(2);
        history.RecordForeground(originalWindow);
        history.RecordForeground(temporaryWindow);
        history.RecordForeground(originalWindow);
        history.RecordDestroyed(temporaryWindow);
        Assert.True(history.TakePending().IsCloseFallback);
    }


    [Fact]
    public void ClosingForegroundWindowDoesNotSuppressDifferentTarget()
    {
        ForegroundWindowTracker.ForegroundTransitionHistory history = new();
        nint originalWindow = new(1);
        nint temporaryWindow = new(2);
        nint launchedWindow = new(3);
        history.RecordForeground(originalWindow);
        history.RecordForeground(temporaryWindow);
        history.RecordDestroyed(temporaryWindow);
        history.RecordForeground(launchedWindow);
        Assert.False(history.TakePending().IsCloseFallback);
    }


    [Fact]
    public void ReturningToPreviousWindowWithoutCloseIsNotFallback()
    {
        ForegroundWindowTracker.ForegroundTransitionHistory history = new();
        nint originalWindow = new(1);
        nint temporaryWindow = new(2);
        history.RecordForeground(originalWindow);
        history.RecordForeground(temporaryWindow);
        history.RecordForeground(originalWindow);
        Assert.False(history.TakePending().IsCloseFallback);
    }


    [Fact]
    public void ClosingBackgroundWindowDoesNotSuppressForegroundChange()
    {
        ForegroundWindowTracker.ForegroundTransitionHistory history = new();
        nint originalWindow = new(1);
        nint currentWindow = new(2);
        nint backgroundWindow = new(3);
        history.RecordForeground(originalWindow);
        history.RecordForeground(currentWindow);
        history.RecordDestroyed(backgroundWindow);
        history.RecordForeground(originalWindow);
        Assert.False(history.TakePending().IsCloseFallback);
    }


    private sealed class TestWindowFocusGuard : IWindowFocusGuard
    {
        public bool IsDirect(nint windowHandle) => false;
    }


    private sealed class TestDispatcher : IDispatcher
    {
        public void Dispatch(Action action) => action();
    }


    private sealed class TestWindowEventListener : IWindowEventListener
    {
        public event Action<IntPtr>? WindowDestroyed;

        public event Action<IntPtr>? ForegroundChanged;

        event Action<IntPtr>? IWindowEventListener.WindowCreated
        {
            add
            {
            }

            remove
            {
            }
        }


        event Action<IntPtr>? IWindowEventListener.WindowShown
        {
            add
            {
            }

            remove
            {
            }
        }


        event Action<IntPtr>? IWindowEventListener.WindowTitleChanged
        {
            add
            {
            }

            remove
            {
            }
        }


        event Action<IntPtr>? IWindowEventListener.WindowLocationChanged
        {
            add
            {
            }

            remove
            {
            }
        }


        event Action<IntPtr>? IWindowEventListener.MinimizeStarted
        {
            add
            {
            }

            remove
            {
            }
        }


        event Action<IntPtr>? IWindowEventListener.MinimizeEnded
        {
            add
            {
            }

            remove
            {
            }
        }


        event Action<IntPtr>? IWindowEventListener.DragStarted
        {
            add
            {
            }

            remove
            {
            }
        }


        event Action<IntPtr>? IWindowEventListener.DragEnded
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


        public void Dispose() => GC.SuppressFinalize(this);

        public void RaiseForegroundChanged(IntPtr handle) => ForegroundChanged?.Invoke(handle);

        public void RaiseWindowDestroyed(IntPtr handle) => WindowDestroyed?.Invoke(handle);

        public void Start()
        {
        }


        public void Stop()
        {
        }
    }
}

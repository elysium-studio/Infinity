using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using Windows.Win32;

namespace Infinity.Platform.Windows;

public sealed class DwmFlushScrollTimer :
    IScrollTimer, 
    IDisposable
{
    private readonly ILogger<DwmFlushScrollTimer> logger;
    private readonly Lock lifecycleLock = new();
    private readonly Thread thread;
    private readonly ManualResetEventSlim activeEvent = new(false);
    private volatile bool running = true;
    private bool disposed;

    public event EventHandler? Tick;

    public DwmFlushScrollTimer(ILogger<DwmFlushScrollTimer> logger)
    {
        this.logger = logger;
        thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "ScrollTimer"
        };

        thread.Start();
    }

    public void Start()
    {
        lock (lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            activeEvent.Set();
        }
    }

    public void Stop()
    {
        lock (lifecycleLock)
        {
            if (!disposed)
            {
                activeEvent.Reset();
            }
        }
    }

    public void Dispose()
    {
        lock (lifecycleLock)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            running = false;
            activeEvent.Set();
        }

        if (Thread.CurrentThread != thread)
        {
            thread.Join();
        }

        activeEvent.Dispose();
        GC.SuppressFinalize(this);
    }

    private void Run()
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        while (running)
        {
            activeEvent.Wait();

            if (!running)
            {
                return;
            }

            try
            {
                PInvoke.DwmFlush();
                Tick?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Scroll timer callback failed");
                activeEvent.Reset();
            }
        }
    }
}

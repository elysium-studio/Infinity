using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using Windows.Win32;

namespace Infinity.Platform.Windows;

public class DwmFlushScrollTimer : 
    IScrollTimer, 
    IDisposable
{
    private readonly ILogger<DwmFlushScrollTimer> logger;
    private readonly Thread thread;
    private readonly ManualResetEventSlim activeEvent = new(false);
    private volatile bool running = true;

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
        logger.LogInformation("ScrollTimer starting");
        activeEvent.Set();
    }

    public void Stop()
    {
        logger.LogInformation("ScrollTimer stopping");
        activeEvent.Reset();
    }

    public void Dispose()
    {
        logger.LogInformation("ScrollTimer disposing");
        running = false;
        activeEvent.Set();
    }

    private void Run()
    {
        logger.LogInformation("ScrollTimer thread started");
        WinRT.ComWrappersSupport.InitializeComWrappers();

        while (running)
        {
            activeEvent.Wait();

            if (!running)
            {
                logger.LogInformation("ScrollTimer thread exiting");
                return;
            }

            PInvoke.DwmFlush();
            Tick?.Invoke(this, EventArgs.Empty);
        }

        logger.LogInformation("ScrollTimer thread exiting");
    }
}
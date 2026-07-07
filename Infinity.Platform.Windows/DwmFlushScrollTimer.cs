using Infinity.Platform.Abstractions;
using Windows.Win32;

namespace Infinity.Platform.Windows;

public class DwmFlushScrollTimer : 
    IScrollTimer, 
    IDisposable
{
    private readonly Thread thread;
    private readonly ManualResetEventSlim activeEvent = new(false);
    private volatile bool running = true;

    public event EventHandler? Tick;

    public DwmFlushScrollTimer()
    {
        thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "ScrollTimer"
        };

        thread.Start();
    }

    public void Start() => activeEvent.Set();

    public void Stop() => activeEvent.Reset();

    public void Dispose()
    {
        running = false;
        activeEvent.Set();
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

            PInvoke.DwmFlush();
            Tick?.Invoke(this, EventArgs.Empty);
        }
    }
}
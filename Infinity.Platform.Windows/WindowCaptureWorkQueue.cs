namespace Infinity.Platform.Windows;

// Serialises native capture work without making the overlay or a native event
// callback wait for rendering, StartCapture, Recreate, or Close.
public sealed class WindowCaptureWorkQueue(Action<Exception> reportFailure)
{
    private readonly object gate = new();
    private readonly Queue<Action> pending = new();
    private bool running;
    private bool completed;

    public bool Enqueue(Action action)
    {
        lock (gate)
        {
            if (completed) return false;
            pending.Enqueue(action);
            StartWorker();
            return true;
        }
    }

    public void Complete(Action cleanup)
    {
        lock (gate)
        {
            if (completed) return;
            completed = true;
            pending.Enqueue(cleanup);
            StartWorker();
        }
    }

    private void StartWorker()
    {
        if (running) return;
        running = true;
        _ = Task.Run(Drain);
    }

    private void Drain()
    {
        while (true)
        {
            Action action;
            lock (gate)
            {
                if (!pending.TryDequeue(out action!))
                {
                    running = false;
                    return;
                }
            }

            try { action(); }
            catch (Exception exception)
            {
                // Reporting must not strand subsequent work (especially cleanup).
                try { reportFailure(exception); }
                catch { }
            }
        }
    }
}

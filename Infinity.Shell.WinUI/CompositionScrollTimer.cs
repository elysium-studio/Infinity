using System;
using System.Threading;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;

namespace Infinity.Shell.WinUI;

public sealed class CompositionScrollTimer(
    DispatcherQueue dispatcherQueue,
    ILogger<CompositionScrollTimer> logger) : IScrollTimer, IDisposable
{
    private int running;
    private int disposed;
    private bool subscribed;

    public event EventHandler? Tick;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        Interlocked.Exchange(ref running, 1);
        UpdateSubscription();
    }

    public void Stop()
    {
        Interlocked.Exchange(ref running, 0);
        UpdateSubscription();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        Stop();
        GC.SuppressFinalize(this);
    }

    private void UpdateSubscription()
    {
        if (dispatcherQueue.HasThreadAccess)
        {
            ApplySubscription();
        }
        else if (!dispatcherQueue.TryEnqueue(ApplySubscription))
        {
            Interlocked.Exchange(ref running, 0);
        }
    }

    private void ApplySubscription()
    {
        bool shouldSubscribe = Volatile.Read(ref running) != 0 && Volatile.Read(ref disposed) == 0;
        if (subscribed == shouldSubscribe)
        {
            return;
        }

        subscribed = shouldSubscribe;
        if (subscribed)
        {
            CompositionTarget.Rendering += HandleRendering;
        }
        else
        {
            CompositionTarget.Rendering -= HandleRendering;
        }
    }

    private void HandleRendering(object? sender, object args)
    {
        if (Volatile.Read(ref running) == 0 || Volatile.Read(ref disposed) != 0)
        {
            return;
        }

        try
        {
            Tick?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            Stop();
            logger.LogError(exception, "Composition scroll callback failed");
        }
    }
}

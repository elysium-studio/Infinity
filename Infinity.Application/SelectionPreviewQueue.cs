using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;

namespace Infinity.Application;

public class SelectionPreviewQueue(IWindowZOrder zOrder) :
    ISelectionPreviewQueue
{
    private readonly object syncRoot = new();
    private CancellationTokenSource? cancellation;
    private IntPtr pendingHandle;

    public void Queue(IntPtr handle, Func<IntPtr> factory)
    {
        CancellationTokenSource previous;
        CancellationTokenSource current = new();

        lock (syncRoot)
        {
            previous = cancellation!;
            cancellation = current;
            pendingHandle = handle;
        }

        previous?.Cancel();

        _ = ProcessAsync(factory, current);
    }

    public void Cancel()
    {
        CancellationTokenSource? current;

        lock (syncRoot)
        {
            current = cancellation;
            cancellation = null;
            pendingHandle = default;
        }

        current?.Cancel();
    }

    private async Task ProcessAsync(Func<IntPtr> factory, CancellationTokenSource cancellationTokenSource)
    {
        try
        {
            await Task.Delay(75, cancellationTokenSource.Token);

            IntPtr handle;

            lock (syncRoot)
            {
                if (!ReferenceEquals(cancellation, cancellationTokenSource))
                {
                    return;
                }

                handle = pendingHandle;
                pendingHandle = default;
                cancellation = null;
            }

            if (handle == default || handle != factory())
            {
                return;
            }

            zOrder.BringToFront(handle);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            cancellationTokenSource.Dispose();
        }
    }
}
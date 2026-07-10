using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;

namespace Infinity.Application;

public class SelectionPreviewQueue(IWindowStack stack,
    ILogger<SelectionPreviewQueue> logger) :
    ISelectionPreviewQueue
{
    private readonly object syncRoot = new();
    private CancellationTokenSource? cancellation;
    private IntPtr pendingHandle;

    public void Queue(IntPtr handle, Func<IntPtr> factory)
    {
        CancellationTokenSource current = new();

        lock (syncRoot)
        {
            cancellation?.Cancel();
            cancellation = current;
            pendingHandle = handle;
        }

        _ = ProcessAsync(factory, current);
    }

    public void Cancel()
    {
        CancellationTokenSource? current;

        lock (syncRoot)
        {
            current = cancellation;
            current?.Cancel();
            cancellation = null;
            pendingHandle = default;
        }
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

            stack.BringToFront(handle);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Selection preview failed");
        }
        finally
        {
            cancellationTokenSource.Dispose();
        }
    }
}

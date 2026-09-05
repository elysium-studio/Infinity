using Infinity.Platform.Windows;

namespace Infinity.Tests;

public sealed class WindowCaptureWorkQueueTests
{
    [Fact]
    public async Task WorkerPreservesExecutionContext()
    {
        AsyncLocal<string?> context = new();
        context.Value = "Capture";
        TaskCompletionSource<string?> observed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        WindowCaptureWorkQueue queue = new(exception => observed.TrySetException(exception));
        queue.Enqueue(() => observed.TrySetResult(context.Value));
        Assert.Equal("Capture", await observed.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task ConcurrentProducersNeverRunCallbacksInParallelAndCleanupRunsLast()
    {
        TaskCompletionSource finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int running = 0;
        int processed = 0;
        WindowCaptureWorkQueue queue = new(exception => finished.TrySetException(exception));
        Task[] producers = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (int index = 0; index < 100; index++)
            {
                Assert.True(queue.Enqueue(() =>
                {
                    Assert.Equal(1, Interlocked.Increment(ref running));
                    processed++;
                    Assert.Equal(0, Interlocked.Decrement(ref running));
                }));
            }
        })).ToArray();
        await Task.WhenAll(producers).WaitAsync(TimeSpan.FromSeconds(5));
        queue.Complete(() =>
        {
            Assert.Equal(800, processed);
            finished.TrySetResult();
        });
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ABlockedCaptureDoesNotBlockTheCallerAndCleanupRunsLast()
    {
        using ManualResetEventSlim release = new();
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<int> order = [];
        WindowCaptureWorkQueue queue = new(exception => finished.TrySetException(exception));
        try
        {
            Assert.True(queue.Enqueue(() =>
            {
                started.TrySetResult();
                release.Wait();
                order.Add(1);
            }));
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(queue.Enqueue(() => order.Add(2)));
            queue.Complete(() =>
            {
                order.Add(3);
                finished.TrySetResult();
            });
            Assert.False(queue.Enqueue(() => order.Add(4)));
            Assert.False(finished.Task.IsCompleted);
        }
        finally
        {
            release.Set();
        }

        await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { 1, 2, 3 }, order);
    }


    [Fact]
    public async Task FailureDoesNotStrandCleanupEvenIfLoggingThrows()
    {
        TaskCompletionSource finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        WindowCaptureWorkQueue queue = new(_ => throw new InvalidOperationException("Logging failed"));
        queue.Enqueue(() => throw new InvalidOperationException("Native capture failed"));
        queue.Complete(() => finished.TrySetResult());
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }


    [Fact]
    public async Task ACallbackCanScheduleShutdownWithoutClosingInsideTheCallback()
    {
        TaskCompletionSource finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        bool callbackReturned = false;
        WindowCaptureWorkQueue queue = new(exception => finished.TrySetException(exception));
        queue.Enqueue(() =>
        {
            queue.Complete(() =>
            {
                Assert.True(callbackReturned);
                finished.TrySetResult();
            });
            callbackReturned = true;
        });
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }
}

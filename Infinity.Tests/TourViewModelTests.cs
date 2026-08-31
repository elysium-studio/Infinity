using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation.Abstractions;
using Infinity.Shell;
using Microsoft.Extensions.Logging.Abstractions;

namespace Infinity.Tests;

public sealed class TourViewModelTests
{
    [Fact]
    public async Task FinishPersistsCompletionBeforeRaisingFinished()
    {
        TestWritableOptions writer = new();
        using TourViewModel viewModel = CreateViewModel(writer);
        TaskCompletionSource<bool> finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        viewModel.Finished += (_, _) => finished.TrySetResult(true);

        viewModel.Finish();

        Assert.False(finished.Task.IsCompleted);
        Assert.Equal(1, writer.WriteCount);
        Assert.False(writer.Value.ShowHintOnStartup);

        writer.Complete();

        await finished.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task FinishContainsPersistenceFailureAndRunsOnlyOnce()
    {
        TestWritableOptions writer = new();
        writer.Fail(new IOException("Write failed"));
        using TourViewModel viewModel = CreateViewModel(writer);
        TaskCompletionSource<bool> finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int finishedCount = 0;
        viewModel.Finished += (_, _) =>
        {
            finishedCount++;
            finished.TrySetResult(true);
        };

        viewModel.Finish();
        viewModel.Finish();

        await finished.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(1, writer.WriteCount);
        Assert.Equal(1, finishedCount);
    }

    private static TourViewModel CreateViewModel(IWritableOptions<Settings> writer) =>
        new(new TestServiceProvider(),
            new TestServiceFactory(),
            new WeakReferenceMessenger(),
            new TestDisposer(),
            writer,
            NullLogger<TourViewModel>.Instance,
            Array.Empty<ITourViewModel>());

    private sealed class TestWritableOptions :
        IWritableOptions<Settings>
    {
        private readonly TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Settings Value { get; } = new();

        public int WriteCount { get; private set; }

        public Task<Settings?> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult<Settings?>(Value);

        public Task WriteAsync(Action<Settings> update, CancellationToken cancellationToken = default)
        {
            WriteCount++;
            update(Value);
            return completion.Task;
        }

        public Task WriteAsync(Settings value, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Complete() => completion.TrySetResult(true);

        public void Fail(Exception exception) => completion.TrySetException(exception);
    }

    private sealed class TestServiceProvider :
        IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class TestServiceFactory :
        IServiceFactory
    {
        public object Create(Type type, Action<object> serviceDelegate, params object?[]? parameters) =>
            throw new NotSupportedException();

        public object Create(Type type, params object?[]? parameters) => throw new NotSupportedException();

        public TService Create<TService>(Action<TService> serviceDelegate, params object?[]? parameters) =>
            throw new NotSupportedException();

        public TService Create<TService>(params object?[]? parameters) => throw new NotSupportedException();
    }

    private sealed class TestDisposer :
        IDisposer
    {
        public void Add(object subject, params object[] objects)
        {
        }

        public TDisposable Replace<TDisposable>(object subject, IDisposable disposer, TDisposable replacement)
            where TDisposable : IDisposable => replacement;

        public void Remove(object subject, IDisposable disposer)
        {
        }

        public void Dispose(object subject)
        {
        }
    }
}
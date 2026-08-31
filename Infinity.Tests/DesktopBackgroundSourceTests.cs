using Infinity.Platform.Windows;
using Microsoft.Extensions.Logging;

namespace Infinity.Tests;

public sealed class DesktopBackgroundSourceTests
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RecoveryPollingInterval = TimeSpan.FromSeconds(30);

    [Fact]
    public void RecoversAfterSnapshotReadFailure()
    {
        TestLogger logger = new();
        Exception? failure = new InvalidOperationException();
        int reads = 0;

        using DesktopBackgroundSource source = CreateSource(logger, () =>
        {
            reads++;

            if (failure is not null)
            {
                throw failure;
            }

            return CreateSnapshot(0x00332211);
        });

        Assert.False(source.PollForChanges());
        Assert.Equal(1, reads);

        failure = null;

        Assert.True(source.PollForChanges());
        Assert.Equal(2, reads);
        Assert.Equal("#112233", source.GetBackground().Colour);
    }

    [Fact]
    public void FailedPollPreservesLastKnownBackground()
    {
        TestLogger logger = new();
        Exception? failure = null;

        using DesktopBackgroundSource source = CreateSource(logger, () =>
        {
            if (failure is not null)
            {
                throw failure;
            }

            return CreateSnapshot(0x00665544);
        });

        Assert.True(source.PollForChanges());
        Assert.Equal("#445566", source.GetBackground().Colour);

        failure = new InvalidOperationException();

        Assert.False(source.PollForChanges());
        Assert.Equal("#445566", source.GetBackground().Colour);
    }

    [Fact]
    public void RepeatedFailuresLogOnlyOnceUntilRecovery()
    {
        TestLogger logger = new();
        Exception? failure = new InvalidOperationException();

        using DesktopBackgroundSource source = CreateSource(logger, () =>
        {
            if (failure is not null)
            {
                throw failure;
            }

            return CreateSnapshot(0x00332211);
        });

        Assert.False(source.PollForChanges());
        Assert.Equal(1, logger.WarningCount);

        Assert.False(source.PollForChanges());
        Assert.Equal(1, logger.WarningCount);

        failure = null;

        Assert.True(source.PollForChanges());
        Assert.Equal(1, logger.InformationCount);

        failure = new InvalidOperationException();

        Assert.False(source.PollForChanges());
        Assert.Equal(2, logger.WarningCount);
    }

    [Fact]
    public void RecoveryNotifiesOnlyWhenBackgroundChanged()
    {
        TestLogger logger = new();
        uint colour = 0x00332211;

        using DesktopBackgroundSource source = CreateSource(logger, () => CreateSnapshot(colour));
        Assert.True(source.PollForChanges());
        int notifications = 0;
        source.BackgroundChanged += (_, _) => notifications++;

        Assert.True(source.PollForChanges());
        Assert.Equal(0, notifications);

        colour = 0x00665544;

        Assert.True(source.PollForChanges());
        Assert.Equal(1, notifications);
        Assert.Equal("#445566", source.GetBackground().Colour);
    }

    [Fact]
    public void ThrowingChangeSubscriberDoesNotTerminatePolling()
    {
        TestLogger logger = new();
        uint colour = 0x00332211;

        using DesktopBackgroundSource source = CreateSource(logger, () => CreateSnapshot(colour));
        Assert.True(source.PollForChanges());
        source.BackgroundChanged += (_, _) => throw new InvalidOperationException();
        colour = 0x00665544;

        Assert.True(source.PollForChanges());
        Assert.Equal(1, logger.ErrorCount);
        Assert.Equal("#445566", source.GetBackground().Colour);
    }

    [Fact]
    public void UnavailableWallpaperDoesNotProduceBlackFallback()
    {
        using DesktopBackgroundSource source = CreateSource(new TestLogger(), () => throw new InvalidOperationException());

        Assert.Null(source.GetBackground().Wallpaper);
        Assert.Null(source.GetBackground().Colour);
        Assert.False(source.PollForChanges());
        Assert.Null(source.GetBackground().Colour);
    }

    [Fact]
    public void DisposalIsIdempotentAndStopsPolling()
    {
        DesktopBackgroundSource source = CreateSource(new TestLogger(), () => CreateSnapshot(0x00332211));

        source.Dispose();
        source.Dispose();

        Assert.False(source.PollForChanges());
    }

    [Fact]
    public async Task StartDoesNotWaitForWallpaperRead()
    {
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using DesktopBackgroundSource source = new(new TestLogger(),
            () =>
            {
                entered.TrySetResult();
                release.Task.GetAwaiter().GetResult();
                return CreateSnapshot(0x00332211);
            },
            PollingInterval,
            RecoveryPollingInterval,
            true);

        try
        {
            await Task.Run(source.Start).WaitAsync(TimeSpan.FromSeconds(1));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(1));
        }
        finally
        {
            release.TrySetResult();
        }

        await WaitUntilAsync(() => source.GetBackground().Colour == "#112233");
    }

    private static DesktopBackgroundSource CreateSource(TestLogger logger,
        Func<DesktopBackgroundSnapshot> snapshotReader) =>
        new(logger,
            snapshotReader,
            PollingInterval,
            RecoveryPollingInterval,
            false);

    private static DesktopBackgroundSnapshot CreateSnapshot(uint colour) =>
        new(string.Empty, colour);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(1));

        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private sealed class TestLogger :
        ILogger<DesktopBackgroundSource>
    {
        public int ErrorCount { get; private set; }

        public int InformationCount { get; private set; }

        public int WarningCount { get; private set; }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error)
            {
                ErrorCount++;
            }
            else if (logLevel == LogLevel.Information)
            {
                InformationCount++;
            }
            else if (logLevel == LogLevel.Warning)
            {
                WarningCount++;
            }
        }
    }
}

using Infinity.Platform.Windows;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace Infinity.Tests;

public sealed class DesktopBackgroundSourceTests
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RecoveryPollingInterval = TimeSpan.FromSeconds(30);

    [Fact]
    public void RecreatesWallpaperSessionAfterComFailure()
    {
        TestLogger logger = new();
        TestDesktopWallpaper failedWallpaper = new() { Failure = CreateServerUnavailableException() };
        TestDesktopWallpaper recoveredWallpaper = new() { Colour = 0x00332211 };
        Queue<DesktopBackgroundSource.IDesktopWallpaper> wallpapers = new([failedWallpaper, recoveredWallpaper]);
        int factoryCalls = 0;

        using DesktopBackgroundSource source = CreateSource(logger, () =>
        {
            factoryCalls++;
            return wallpapers.Dequeue();
        });

        Assert.Equal(1, factoryCalls);
        Assert.True(source.PollForChanges());
        Assert.Equal(2, factoryCalls);
        Assert.Equal("#112233", source.GetBackground().Colour);
    }

    [Fact]
    public void FailedPollPreservesLastKnownBackground()
    {
        TestLogger logger = new();
        TestDesktopWallpaper wallpaper = new() { Colour = 0x00665544 };

        using DesktopBackgroundSource source = CreateSource(logger, () => wallpaper);

        Assert.Equal("#445566", source.GetBackground().Colour);

        wallpaper.Failure = CreateServerUnavailableException();

        Assert.False(source.PollForChanges());
        Assert.Equal("#445566", source.GetBackground().Colour);
    }

    [Fact]
    public void RepeatedFailuresLogOnlyOnceUntilRecovery()
    {
        TestLogger logger = new();
        TestDesktopWallpaper wallpaper = new() { Failure = CreateServerUnavailableException() };

        using DesktopBackgroundSource source = CreateSource(logger, () => wallpaper);

        Assert.False(source.PollForChanges());
        Assert.Equal(1, logger.WarningCount);

        wallpaper.Failure = null;

        Assert.True(source.PollForChanges());
        Assert.Equal(1, logger.InformationCount);

        wallpaper.Failure = CreateServerUnavailableException();

        Assert.False(source.PollForChanges());
        Assert.Equal(2, logger.WarningCount);
    }

    [Fact]
    public void RecoveryNotifiesOnlyWhenBackgroundChanged()
    {
        TestLogger logger = new();
        TestDesktopWallpaper wallpaper = new() { Colour = 0x00332211 };

        using DesktopBackgroundSource source = CreateSource(logger, () => wallpaper);
        int notifications = 0;
        source.BackgroundChanged += (_, _) => notifications++;

        Assert.True(source.PollForChanges());
        Assert.Equal(0, notifications);

        wallpaper.Colour = 0x00665544;

        Assert.True(source.PollForChanges());
        Assert.Equal(1, notifications);
        Assert.Equal("#445566", source.GetBackground().Colour);
    }

    [Fact]
    public void ThrowingChangeSubscriberDoesNotTerminatePolling()
    {
        TestLogger logger = new();
        TestDesktopWallpaper wallpaper = new() { Colour = 0x00332211 };

        using DesktopBackgroundSource source = CreateSource(logger, () => wallpaper);
        source.BackgroundChanged += (_, _) => throw new InvalidOperationException();
        wallpaper.Colour = 0x00665544;

        Assert.True(source.PollForChanges());
        Assert.Equal(1, logger.ErrorCount);
        Assert.Equal("#445566", source.GetBackground().Colour);
    }

    [Fact]
    public void DisposalIsIdempotentAndStopsPolling()
    {
        TestLogger logger = new();
        TestDesktopWallpaper wallpaper = new() { Colour = 0x00332211 };
        DesktopBackgroundSource source = CreateSource(logger, () => wallpaper);

        source.Dispose();
        source.Dispose();

        Assert.False(source.PollForChanges());
    }

    private static DesktopBackgroundSource CreateSource(TestLogger logger,
        Func<DesktopBackgroundSource.IDesktopWallpaper> wallpaperFactory) =>
        new(logger,
            wallpaperFactory,
            PollingInterval,
            RecoveryPollingInterval,
            false);

    private static COMException CreateServerUnavailableException() =>
        new("The RPC server is unavailable", unchecked((int)0x800706BA));

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

    private sealed class TestDesktopWallpaper :
        DesktopBackgroundSource.IDesktopWallpaper
    {
        public uint Colour { get; set; }

        public COMException? Failure { get; set; }

        public void SetWallpaper(string monitorID, string wallpaper) =>
            throw new NotSupportedException();

        public string GetWallpaper(string monitorID) => string.Empty;

        public string GetMonitorDevicePathAt(uint monitorIndex)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            return "Monitor";
        }

        public uint GetMonitorDevicePathCount() =>
            throw new NotSupportedException();

        public DesktopBackgroundSource.RECT GetMonitorRECT(string monitorID) =>
            throw new NotSupportedException();

        public void SetBackgroundColor(uint color) =>
            throw new NotSupportedException();

        public uint GetBackgroundColor() => Colour;

        public void SetPosition(int position) =>
            throw new NotSupportedException();

        public int GetPosition() =>
            throw new NotSupportedException();

        public void SetSlideshow(nint items) =>
            throw new NotSupportedException();

        public void GetSlideshow(out nint items) =>
            throw new NotSupportedException();

        public void SetSlideshowOptions(int options, uint slideshowTick) =>
            throw new NotSupportedException();

        public void GetSlideshowOptions(out int options, out uint slideshowTick) =>
            throw new NotSupportedException();

        public void AdvanceSlideshow(string monitorID, int direction) =>
            throw new NotSupportedException();

        public void GetStatus(out int state) =>
            throw new NotSupportedException();

        public bool Enable() =>
            throw new NotSupportedException();
    }
}

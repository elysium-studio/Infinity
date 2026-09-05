using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;

namespace Infinity.Platform.Windows;

public sealed class DesktopBackgroundSource : IDesktopBackgroundSource, IDisposable
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RecoveryPollingInterval = TimeSpan.FromSeconds(30);
    private readonly Lock lifecycleLock = new();
    private readonly Timer changeTimer;
    private readonly ILogger<DesktopBackgroundSource> logger;
    private readonly Func<DesktopBackgroundSnapshot> snapshotReader;
    private readonly TimeSpan pollingInterval;
    private readonly TimeSpan recoveryPollingInterval;
    private readonly bool pollingEnabled;
    private DesktopBackgroundSnapshot? snapshot;
    private bool pollingFailed;
    private bool isStarted;
    private volatile bool disposed;

    public event EventHandler? BackgroundChanged;

    public DesktopBackgroundSource(ILogger<DesktopBackgroundSource> logger, DesktopWallpaperSnapshotReader snapshotReader) : this(logger, snapshotReader.Read, PollingInterval, RecoveryPollingInterval, true)
    {
    }


    internal DesktopBackgroundSource(ILogger<DesktopBackgroundSource> logger, Func<DesktopBackgroundSnapshot> snapshotReader, TimeSpan pollingInterval, TimeSpan recoveryPollingInterval, bool pollingEnabled)
    {
        this.logger = logger;
        this.snapshotReader = snapshotReader;
        this.pollingInterval = pollingInterval;
        this.recoveryPollingInterval = recoveryPollingInterval;
        this.pollingEnabled = pollingEnabled;
        changeTimer = new(CheckForChanges, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }


    public DesktopBackground GetBackground()
    {
        DesktopBackgroundSnapshot? current = Volatile.Read(ref snapshot);
        if (current is null)
        {
            return new();
        }

        if (!string.IsNullOrEmpty(current.WallpaperPath) && File.Exists(current.WallpaperPath))
        {
            return new()
            {
                Wallpaper = current.WallpaperPath
            };
        }

        uint colour = current.Colour;
        byte r = (byte)(colour & 0xFF);
        byte g = (byte)((colour >> 8) & 0xFF);
        byte b = (byte)((colour >> 16) & 0xFF);
        return new()
        {
            Colour = $"#{r:X2}{g:X2}{b:X2}"};
    }


    public void Start()
    {
        lock (lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (isStarted)
            {
                return;
            }

            isStarted = true;
            ScheduleNextPollCore(TimeSpan.Zero);
        }
    }


    public void Stop()
    {
        lock (lifecycleLock)
        {
            if (!isStarted)
            {
                return;
            }

            isStarted = false;
            changeTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }


    internal bool PollForChanges()
    {
        if (disposed)
        {
            return false;
        }

        DesktopBackgroundSnapshot current;
        try
        {
            current = snapshotReader();
        }
        catch (Exception exception)
        {
            if (!disposed && !pollingFailed)
            {
                logger.LogWarning(exception, "Desktop background polling failed; retrying at a reduced cadence");
            }

            pollingFailed = true;
            ScheduleNextPoll(recoveryPollingInterval);
            return false;
        }

        if (disposed)
        {
            return false;
        }

        if (pollingFailed)
        {
            logger.LogInformation("Desktop background polling recovered");
            pollingFailed = false;
        }

        DesktopBackgroundSnapshot? previous = Volatile.Read(ref snapshot);
        if (current != previous)
        {
            Volatile.Write(ref snapshot, current);
            try
            {
                BackgroundChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Desktop background change callback failed");
            }
        }

        ScheduleNextPoll(pollingInterval);
        return true;
    }


    private void CheckForChanges(object? state) => _ = PollForChanges();

    private void ScheduleNextPoll(TimeSpan dueTime)
    {
        lock (lifecycleLock)
        {
            ScheduleNextPollCore(dueTime);
        }
    }


    private void ScheduleNextPollCore(TimeSpan dueTime)
    {
        if (pollingEnabled && isStarted && !disposed)
        {
            changeTimer.Change(dueTime, Timeout.InfiniteTimeSpan);
        }
    }


    public void Dispose()
    {
        lock (lifecycleLock)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            isStarted = false;
            changeTimer.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}

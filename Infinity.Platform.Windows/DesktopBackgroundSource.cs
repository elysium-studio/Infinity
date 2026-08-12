using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace Infinity.Platform.Windows;

public sealed unsafe partial class DesktopBackgroundSource :
    IDesktopBackgroundSource,
    IDisposable
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RecoveryPollingInterval = TimeSpan.FromSeconds(30);

    private const uint SpiGetDesktopWallpaper = 0x0073;
    private const int ColorDesktop = 1;
    private const int MaximumWallpaperPathLength = 260;

    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SystemParametersInfo(uint action, uint parameter, char* value, uint update);

    [LibraryImport("user32.dll")]
    private static partial uint GetSysColor(int index);

    private readonly Lock lifecycleLock = new();
    private readonly System.Threading.Timer changeTimer;
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

    public DesktopBackgroundSource(ILogger<DesktopBackgroundSource> logger) :
        this(logger,
            ReadSystemSnapshot,
            PollingInterval,
            RecoveryPollingInterval,
            true)
    {
    }

    internal DesktopBackgroundSource(ILogger<DesktopBackgroundSource> logger,
        Func<DesktopBackgroundSnapshot> snapshotReader,
        TimeSpan pollingInterval,
        TimeSpan recoveryPollingInterval,
        bool pollingEnabled)
    {
        this.logger = logger;
        this.snapshotReader = snapshotReader;
        this.pollingInterval = pollingInterval;
        this.recoveryPollingInterval = recoveryPollingInterval;
        this.pollingEnabled = pollingEnabled;
        changeTimer = new Timer(CheckForChanges, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public DesktopBackground GetBackground()
    {
        DesktopBackgroundSnapshot? current = Volatile.Read(ref snapshot);

        if (current is null)
        {
            return new DesktopBackground();
        }

        if (!string.IsNullOrEmpty(current.WallpaperPath) && File.Exists(current.WallpaperPath))
        {
            return new DesktopBackground { Wallpaper = current.WallpaperPath };
        }

        uint colour = current.Colour;
        byte r = (byte)(colour & 0xFF);
        byte g = (byte)((colour >> 8) & 0xFF);
        byte b = (byte)((colour >> 16) & 0xFF);

        return new DesktopBackground { Colour = $"#{r:X2}{g:X2}{b:X2}" };
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

    private static DesktopBackgroundSnapshot ReadSystemSnapshot()
    {
        Span<char> pathBuffer = stackalloc char[MaximumWallpaperPathLength];
        string wallpaperPath = string.Empty;

        fixed (char* path = pathBuffer)
        {
            if (SystemParametersInfo(SpiGetDesktopWallpaper,
                MaximumWallpaperPathLength,
                path,
                0))
            {
                wallpaperPath = new string(path);
            }
        }

        return new DesktopBackgroundSnapshot(wallpaperPath, GetSysColor(ColorDesktop));
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

    private void CheckForChanges(object? state)
    {
        _ = PollForChanges();
    }

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

    internal sealed record DesktopBackgroundSnapshot(string WallpaperPath, uint Colour);
}

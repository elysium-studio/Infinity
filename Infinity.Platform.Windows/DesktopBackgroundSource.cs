using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Infinity.Platform.Windows;

public sealed unsafe partial class DesktopBackgroundSource :
    IDesktopBackgroundSource,
    IDisposable
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RecoveryPollingInterval = TimeSpan.FromSeconds(30);

    private const int ShutdownTimeoutMilliseconds = 2000;

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [GeneratedComInterface]
    [Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal partial interface IDesktopWallpaper
    {
        void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitorID, [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);

        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitorID);

        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetMonitorDevicePathAt(uint monitorIndex);

        [return: MarshalAs(UnmanagedType.U4)]
        uint GetMonitorDevicePathCount();

        RECT GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitorID);

        void SetBackgroundColor([MarshalAs(UnmanagedType.U4)] uint color);

        [return: MarshalAs(UnmanagedType.U4)]
        uint GetBackgroundColor();

        void SetPosition([MarshalAs(UnmanagedType.I4)] int position);

        [return: MarshalAs(UnmanagedType.I4)]
        int GetPosition();

        void SetSlideshow(nint items);

        void GetSlideshow(out nint items);

        void SetSlideshowOptions(int options, uint slideshowTick);

        void GetSlideshowOptions(out int options, out uint slideshowTick);

        void AdvanceSlideshow([MarshalAs(UnmanagedType.LPWStr)] string monitorID, [MarshalAs(UnmanagedType.I4)] int direction);

        void GetStatus([MarshalAs(UnmanagedType.I4)] out int state);

        [return: MarshalAs(UnmanagedType.Bool)]
        bool Enable();
    }

    private static readonly Guid clsidDesktopWallpaper = new("C2CF3110-460E-4fc1-B9D0-8A1C0C9CC4BD");
    private static readonly Guid iidDesktopWallpaper = new("B92B56A9-8B55-4E14-9A89-0199BBB6F93B");

    private const uint ClsctxLocalServer = 4;

    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(in Guid rclsid, nint pUnkOuter, uint dwClsContext, in Guid riid,
 out nint ppv);

    private readonly BlockingCollection<Action> workQueue = new();
    private readonly Lock lifecycleLock = new();
    private readonly Thread comThread;
    private readonly System.Threading.Timer changeTimer;
    private readonly ILogger<DesktopBackgroundSource> logger;
    private readonly Func<IDesktopWallpaper> wallpaperFactory;
    private readonly TimeSpan pollingInterval;
    private readonly TimeSpan recoveryPollingInterval;
    private readonly bool pollingEnabled;
    private IDesktopWallpaper? wallpaper;
    private DesktopBackgroundSnapshot snapshot = DesktopBackgroundSnapshot.Empty;
    private bool pollingFailed;
    private volatile bool disposed;

    public event EventHandler? BackgroundChanged;

    public DesktopBackgroundSource(ILogger<DesktopBackgroundSource> logger) :
        this(logger,
            CreateDesktopWallpaper,
            PollingInterval,
            RecoveryPollingInterval,
            true)
    {
    }

    internal DesktopBackgroundSource(ILogger<DesktopBackgroundSource> logger,
        Func<IDesktopWallpaper> wallpaperFactory,
        TimeSpan pollingInterval,
        TimeSpan recoveryPollingInterval,
        bool pollingEnabled)
    {
        this.logger = logger;
        this.wallpaperFactory = wallpaperFactory;
        this.pollingInterval = pollingInterval;
        this.recoveryPollingInterval = recoveryPollingInterval;
        this.pollingEnabled = pollingEnabled;

        comThread = new Thread(() =>
        {
            try
            {
                foreach (Action action in workQueue.GetConsumingEnumerable())
                {
                    action();
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        comThread.SetApartmentState(ApartmentState.STA);
        comThread.IsBackground = true;
        comThread.Name = "DesktopBackgroundSource";
        comThread.Start();

        changeTimer = new Timer(CheckForChanges, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        PollForChanges(false);
    }

    public DesktopBackground GetBackground()
    {
        DesktopBackgroundSnapshot current = Volatile.Read(ref snapshot);

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

    private static IDesktopWallpaper CreateDesktopWallpaper()
    {
        int hr = CoCreateInstance(in clsidDesktopWallpaper,
            nint.Zero,
            ClsctxLocalServer,
            in iidDesktopWallpaper,
            out nint ppv);

        if (hr < 0)
        {
            Marshal.ThrowExceptionForHR(hr);
        }

        return ComInterfaceMarshaller<IDesktopWallpaper>.ConvertToManaged((void*)ppv)!;
    }

    private T RunOnComThread<T>(Func<T> func)
    {
        T result = default!;
        Exception? error = null;
        using ManualResetEventSlim completed = new(false);

        lock (lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(disposed, this);

            workQueue.Add(() =>
            {
                try
                {
                    result = func();
                }
                catch (Exception exception)
                {
                    error = exception;
                }
                finally
                {
                    completed.Set();
                }
            });
        }

        completed.Wait();

        if (error is not null)
        {
            ExceptionDispatchInfo.Capture(error).Throw();
        }

        return result;
    }

    private DesktopBackgroundSnapshot ReadSnapshotCore()
    {
        try
        {
            wallpaper ??= wallpaperFactory();
            string wallpaperPath = wallpaper.GetWallpaper(wallpaper.GetMonitorDevicePathAt(0));
            uint colour = wallpaper.GetBackgroundColor();
            return new DesktopBackgroundSnapshot(wallpaperPath, colour);
        }
        catch (COMException)
        {
            wallpaper = null;
            throw;
        }
    }

    internal bool PollForChanges() => PollForChanges(true);

    private bool PollForChanges(bool raiseChanged)
    {
        DesktopBackgroundSnapshot current;

        try
        {
            current = RunOnComThread(ReadSnapshotCore);
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

        if (pollingFailed)
        {
            logger.LogInformation("Desktop background polling recovered");
            pollingFailed = false;
        }

        DesktopBackgroundSnapshot previous = Volatile.Read(ref snapshot);

        if (current != previous)
        {
            Volatile.Write(ref snapshot, current);

            if (raiseChanged)
            {
                try
                {
                    BackgroundChanged?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Desktop background change callback failed");
                }
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
        if (!pollingEnabled)
        {
            return;
        }

        lock (lifecycleLock)
        {
            if (!disposed)
            {
                changeTimer.Change(dueTime, Timeout.InfiniteTimeSpan);
            }
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
            changeTimer.Dispose();
            workQueue.Add(() => wallpaper = null);
            workQueue.CompleteAdding();
        }

        if (Thread.CurrentThread == comThread)
        {
            GC.SuppressFinalize(this);
            return;
        }

        if (comThread.Join(ShutdownTimeoutMilliseconds))
        {
            workQueue.Dispose();
        }
        else
        {
            logger.LogWarning("Desktop background worker did not stop within the shutdown timeout");
        }

        GC.SuppressFinalize(this);
    }

    private sealed record DesktopBackgroundSnapshot(string WallpaperPath, uint Colour)
    {
        public static DesktopBackgroundSnapshot Empty { get; } = new(string.Empty, 0);
    }
}

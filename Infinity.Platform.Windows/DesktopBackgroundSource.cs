using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Infinity.Platform.Windows;

public unsafe partial class DesktopBackgroundSource :
    IDesktopBackgroundSource,
    IDisposable
{
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
    private readonly Thread comThread;
    private readonly System.Threading.Timer changeTimer;
    private readonly ILogger<DesktopBackgroundSource> logger;
    private IDesktopWallpaper? wallpaper;
    private string lastWallpaperPath = string.Empty;
    private uint lastColour;
    private bool disposed;

    public event EventHandler? BackgroundChanged;

    public DesktopBackgroundSource(ILogger<DesktopBackgroundSource> logger)
    {
        this.logger = logger;

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

        lastWallpaperPath = RunOnComThread(GetWallpaperPathCore);
        lastColour = RunOnComThread(GetBackgroundColourCore);
        changeTimer = new Timer(CheckForChanges, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
    }

    public DesktopBackground GetBackground()
    {
        string path = RunOnComThread(GetWallpaperPathCore);

        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            return new DesktopBackground { Wallpaper = path };
        }

        uint colour = RunOnComThread(GetBackgroundColourCore);
        byte r = (byte)(colour & 0xFF);
        byte g = (byte)((colour >> 8) & 0xFF);
        byte b = (byte)((colour >> 16) & 0xFF);

        return new DesktopBackground { Colour = $"#{r:X2}{g:X2}{b:X2}" };
    }

    private static IDesktopWallpaper CreateDesktopWallpaper()
    {
        int hr = CoCreateInstance(
            in clsidDesktopWallpaper,
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
        if (disposed)
        {
            return default!;
        }

        T result = default!;
        ManualResetEventSlim completed = new(false);

        try
        {
            workQueue.Add(() =>
            {
                result = func();
                completed.Set();
            });

            completed.Wait();
        }
        catch (InvalidOperationException)
        {
        }
        catch (OperationCanceledException)
        {
        }

        return result;
    }

    private string GetWallpaperPathCore()
    {
        try
        {
            wallpaper ??= CreateDesktopWallpaper();
            return wallpaper.GetWallpaper(wallpaper.GetMonitorDevicePathAt(0));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get wallpaper path");
            return string.Empty;
        }
    }

    private uint GetBackgroundColourCore()
    {
        try
        {
            wallpaper ??= CreateDesktopWallpaper();
            return wallpaper.GetBackgroundColor();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get background colour");
            return 0;
        }
    }

    private void CheckForChanges(object? state)
    {
        if (disposed)
        {
            return;
        }

        try
        {
            string currentPath = RunOnComThread(GetWallpaperPathCore);
            uint currentColour = RunOnComThread(GetBackgroundColourCore);

            if (currentPath != lastWallpaperPath || currentColour != lastColour)
            {
                lastWallpaperPath = currentPath;
                lastColour = currentColour;
                BackgroundChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to check for background changes");
        }
    }

    public void Dispose()
    {
        disposed = true;
        changeTimer.Dispose();
        workQueue.CompleteAdding();
    }
}
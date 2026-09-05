using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace Infinity.UI.WinUI;

internal sealed class DesktopOverlayMonitorTopology
{
    private readonly List<DesktopOverlayMonitor> monitors = EnumerateMonitors();

    public IReadOnlyList<DesktopOverlayMonitor> Monitors => monitors;

    public DesktopOverlayMonitorSpan ResolveSpan(HMONITOR activeMonitor, bool spanningEnabled)
    {
        int monitorIndex = monitors.FindIndex(monitor => monitor.Handle == activeMonitor);
        if (monitorIndex < 0)
        {
            return new(default, new HashSet<nint>());
        }

        DesktopOverlayMonitor activeDisplay = monitors[monitorIndex];
        if (!spanningEnabled)
        {
            return CreateSpan([activeDisplay]);
        }

        List<DesktopOverlayMonitor> compatible = [..monitors.Where(monitor => IsCompatible(activeDisplay, monitor)).OrderBy(monitor => monitor.Bounds.X)];
        int activeIndex = compatible.FindIndex(monitor => monitor.Handle == activeMonitor);
        if (activeIndex < 0)
        {
            return CreateSpan([activeDisplay]);
        }

        int firstIndex = activeIndex;
        int lastIndex = activeIndex;
        while (firstIndex > 0 && Right(compatible[firstIndex - 1].Bounds) == compatible[firstIndex].Bounds.X)
        {
            firstIndex--;
        }

        while (lastIndex < compatible.Count - 1 && Right(compatible[lastIndex].Bounds) == compatible[lastIndex + 1].Bounds.X)
        {
            lastIndex++;
        }

        return CreateSpan(compatible.GetRange(firstIndex, lastIndex - firstIndex + 1));
    }


    private static DesktopOverlayMonitorSpan CreateSpan(IReadOnlyList<DesktopOverlayMonitor> monitors)
    {
        if (monitors.Count == 0)
        {
            return new(default, new HashSet<nint>());
        }

        RectInt32 first = monitors[0].Bounds;
        RectInt32 last = monitors[^1].Bounds;
        RectInt32 bounds = new(first.X, first.Y, Right(last) - first.X, first.Height);
        HashSet<nint> handles = [..monitors.Select(monitor => (nint)monitor.Handle)];
        return new(bounds, handles);
    }


    private static bool IsCompatible(DesktopOverlayMonitor active, DesktopOverlayMonitor candidate) => candidate.Bounds.Width == active.Bounds.Width && candidate.Bounds.Height == active.Bounds.Height && candidate.Bounds.Y == active.Bounds.Y && candidate.DpiX == active.DpiX && candidate.DpiY == active.DpiY;

    private static int Right(RectInt32 bounds) => bounds.X + bounds.Width;

    private static unsafe List<DesktopOverlayMonitor> EnumerateMonitors()
    {
        List<DesktopOverlayMonitor> results = [];
        PInvoke.EnumDisplayMonitors(HDC.Null, null, (monitor, deviceContext, rect, data) =>  {  MONITORINFO info = new()  {  cbSize = (uint)Marshal.SizeOf<MONITORINFO>()  };  if (!PInvoke.GetMonitorInfo(monitor, ref info))  {  return true;  }   uint dpiX = 96;  uint dpiY = 96;  _ = PInvoke.GetDpiForMonitor(monitor, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out dpiX, out dpiY);  if (dpiX == 0 || dpiY == 0)  {  dpiX = 96;  dpiY = 96;  }   RectInt32 bounds = new(info.rcMonitor.left, info.rcMonitor.top, info.rcMonitor.right - info.rcMonitor.left, info.rcMonitor.bottom - info.rcMonitor.top);  results.Add(new DesktopOverlayMonitor(monitor, bounds, dpiX, dpiY));  return true;  }, new LPARAM(0));
        return results;
    }
}
